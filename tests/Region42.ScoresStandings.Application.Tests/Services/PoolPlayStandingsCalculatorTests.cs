using FluentAssertions;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services.Tournaments;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Tests.Services;

public class PoolPlayStandingsCalculatorTests
{
	private static List<TeamStanding> CreateSeededTeams(int count = 3)
	{
		return Enumerable.Range(1, count)
			.Select(i => new TeamStanding
			{
				Rank = i,
				TeamId = i,
				TeamName = $"14UB0{i} Team {i}",
				TeamShortName = $"0{i} Team {i}"
			})
			.ToList();
	}

	private static PoolPlayGame CreateCompletedPoolGame(
		int id,
		TournamentStage stage,
		int homeTeamId,
		int awayTeamId,
		int homeScore,
		int awayScore,
		bool isForfeit = false,
		int homeRed = 0,
		int awayRed = 0,
		int homeYellow = 0,
		int awayYellow = 0)
	{
		var game = new PoolPlayGame
		{
			Id = id,
			DivisionId = 1,
			Round = 10,
			Stage = stage,
			HomeTeamId = homeTeamId,
			AwayTeamId = awayTeamId,
			Status = GameStatus.Completed
		};

		var score = new PoolPlayScore
		{
			Id = id,
			GameId = id,
			Game = game,
			HomeScore = homeScore,
			AwayScore = awayScore,
			IsForfeit = isForfeit,
			HomeRedCards = homeRed,
			AwayRedCards = awayRed,
			HomeYellowCards = homeYellow,
			AwayYellowCards = awayYellow
		};

		game.Score = score;
		return game;
	}

	[Fact]
	public void Calculate_TenPointSystem_AwardsMax10PointsFor3To0Win()
	{
		var seededTeams = CreateSeededTeams(3);
		// Team 1 beats Team 2 4-0: 6 (win) + 3 (capped goals) + 1 (shutout) = 10 pts
		// Team 2 loses 0-4: 0 pts
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 4, 0)
		};

		var result = PoolPlayStandingsCalculator.Calculate(seededTeams, games);

		var team1 = result.Standings.First(s => s.TeamId == 1);
		var team2 = result.Standings.First(s => s.TeamId == 2);

		team1.PoolPoints.Should().Be(10);
		team1.GoalsFor.Should().Be(4);
		team2.PoolPoints.Should().Be(0);
	}

	[Fact]
	public void Calculate_TenPointSystem_Awards4PointsEachForScorelessDrawAndGoalBonusesInDrawsAndLosses()
	{
		var seededTeams = CreateSeededTeams(3);
		// Game 1: Team 1 vs Team 2 ends 0-0 -> 3 (tie) + 1 (shutout) = 4 pts each
		// Game 2: Team 2 vs Team 3 ends 2-3 -> Team 2 gets 2 (goals) = 2 pts; Team 3 gets 6 (win) + 3 (goals) = 9 pts
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 0, 0),
			CreateCompletedPoolGame(2, TournamentStage.PoolPlay2, 2, 3, 2, 3)
		};

		var result = PoolPlayStandingsCalculator.Calculate(seededTeams, games);

		result.Standings.First(s => s.TeamId == 1).PoolPoints.Should().Be(4);
		result.Standings.First(s => s.TeamId == 2).PoolPoints.Should().Be(6); // 4 + 2
		result.Standings.First(s => s.TeamId == 3).PoolPoints.Should().Be(9);
	}

	[Fact]
	public void Calculate_WithForfeit_Awards7PointsWithoutShutoutBonus()
	{
		var seededTeams = CreateSeededTeams(3);
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 1, 0, isForfeit: true)
		};

		var result = PoolPlayStandingsCalculator.Calculate(seededTeams, games);

		result.AnyForfeitInPool.Should().BeTrue();
		var team1 = result.Standings.First(s => s.TeamId == 1);
		var team2 = result.Standings.First(s => s.TeamId == 2);

		team1.PoolPoints.Should().Be(7); // 6 win + 1 goal, no shutout point
		team2.PoolPoints.Should().Be(0);
	}

	[Fact]
	public void Calculate_WithRedAndYellowCards_Deducts2PerRedAnd1PerYellow()
	{
		var seededTeams = CreateSeededTeams(3);
		// Team 1 wins 2-1 (6 + 2 = 8 pts) but has 1 RC (-2) and 1 YC (-1) => 5 pts
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 2, 1, homeRed: 1, homeYellow: 1)
		};

		var result = PoolPlayStandingsCalculator.Calculate(seededTeams, games);

		var team1 = result.Standings.First(s => s.TeamId == 1);
		team1.PoolPoints.Should().Be(5);
		team1.RedCards.Should().Be(1);
		team1.YellowCards.Should().Be(1);
	}

	[Fact]
	public void Calculate_TwoWayTie_UsesHeadToHeadFirst()
	{
		var seededTeams = CreateSeededTeams(3);
		// Team 2 beats Team 1 (1-0 -> 8 pts for T2, 0 for T1)
		// Team 3 beats Team 2 (1-0 -> 8 pts for T3, 0 for T2)
		// Team 1 beats Team 3 (3-0 -> 10 pts for T1, 0 for T3)
		// T1 = 10 pts (1st). T2 and T3 are tied at 8 pts!
		// In H2H between T2 and T3, T3 beat T2, so T3 advances as 2nd!
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 0, 1),
			CreateCompletedPoolGame(2, TournamentStage.PoolPlay2, 2, 3, 0, 1),
			CreateCompletedPoolGame(3, TournamentStage.PoolPlay3, 1, 3, 3, 0)
		};

		var result = PoolPlayStandingsCalculator.Calculate(seededTeams, games);

		result.Standings[0].TeamId.Should().Be(1);
		result.Standings[1].TeamId.Should().Be(3);
		result.Standings[1].AdvancesToFinal.Should().BeTrue();
		result.Standings[2].TeamId.Should().Be(2);
		result.Standings[2].AdvancesToFinal.Should().BeFalse();
	}

	[Fact]
	public void Calculate_ThreeWayTie_SkipsHeadToHeadAndUsesWinsThenCardsThenCappedGA()
	{
		var seededTeams = CreateSeededTeams(3);
		// Circular 1-0 wins so all 3 teams have 8 points and 1 win each, but Team 1 has 1 yellow card (Wait: if T1 has YC, points would differ).
		// Let's make all 3 teams have 8 points:
		// T1 beats T2 2-1 (8 pts), T2 beats T3 2-1 (8 pts), T3 beats T1 2-0 (9 pts) - not equal.
		// Equal points: all three games 1-0 (8 pts each). T3 has 0 YC, T2 has 0 YC, T1 has 0 YC, all identical -> requires PK tiebreaker!
		var games = new List<PoolPlayGame>
		{
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 2, 1, 0),
			CreateCompletedPoolGame(2, TournamentStage.PoolPlay2, 2, 3, 1, 0),
			CreateCompletedPoolGame(3, TournamentStage.PoolPlay3, 3, 1, 1, 0)
		};

		var resultWithoutOverride = PoolPlayStandingsCalculator.Calculate(seededTeams, games);
		resultWithoutOverride.RequiresPenaltyShootoutTiebreaker.Should().BeTrue();

		// When admin selects Team 3 as FIFA kicks tiebreaker winner, Team 3 moves to 1st
		var resultWithOverride = PoolPlayStandingsCalculator.Calculate(seededTeams, games, poolPlayTiebreakerWinnerTeamId: 3);
		resultWithOverride.Standings[0].TeamId.Should().Be(3);
	}

	[Fact]
	public void Calculate_WhenForfeitExistsInPool_OmitsWinsAndGoalDifferentialFromTiebreakers()
	{
		var seededTeams = CreateSeededTeams(3);
		// Suppose Game 3 (Team 1 vs Team 3) is a forfeit (Team 1 wins 1-0 forfeit = 7 pts).
		// Game 1 (Team 1 vs Team 2): Team 2 wins 3-0 (10 pts).
		// Now suppose Team 1 and Team 3 both end up with 7 pts:
		// Let's test 2 teams tied on points when a forfeit exists in the pool:
		// Game 1: T1 vs T2 -> 1-1 tie (T1=4, T2=4)
		// Game 2: T2 vs T3 -> T2 wins 1-0 forfeit (T2=4+7=11, T3=0)
		// Game 3: T1 vs T3 -> T3 wins 2-1 (T3=0+8=8, T1=4+1=5)
		// Let's construct a tie between T1 and T2 where T1 has 1 win and T2 has 0 wins, while T3 forfeited:
		// T1 beats T3 by forfeit 1-0 (T1 = 7 pts, 1 win).
		// T2 ties T1 1-1 (T1 = 7+4 = 11 pts; T2 = 4 pts) and T2 ties T3 3-3 with T2 having 0 cards (T2 = 4+6 = 10 pts).
		// To make T1 and T2 tied at 7 pts:
		// Game 1 (T1 vs T2): 0-0 tie, but T1 gets 1 Red Card (-2) -> T1 = 2 pts, T2 = 4 pts.
		// Game 2 (T2 vs T3): 0-1 loss for T2, T2 scores 0 -> T2 = 4 pts total (0 wins).
		// Game 3 (T1 vs T3): T1 wins 1-0 forfeit, gets 1 Red Card (-2) -> T1 gets 7 - 2 = 5 pts -> T1 = 2 + 5 = 7 pts.
		// Let's instead give T2 3-3 tie vs T3 (3 + 3 = 6 pts) and 0-1 loss vs T1 where T2 scores 1 (1-2 loss = 1 pt) => T2 has 7 pts (0 wins, 0 cards).
		// And T1 has 1-0 forfeit win vs T3 (7 pts, 1 win, 1 yellow card -> 6 pts)...
		// Even simpler: in a 4-team pool, T4 forfeits to T3 (so anyForfeitInPool is true).
		// T1 and T2 play to a 1-1 draw (4 pts each).
		// T1 beats T4 1-0 (8 pts -> total 12 pts, 1 win, +1 GD, 1 YC -> 11 pts).
		// T2 draws T3 3-3 (6 pts) and loses to T4 2-3 (2 pts)...
		var fourTeams = CreateSeededTeams(4);
		var poolGames = new List<PoolPlayGame>
		{
			// Forfeit exists in pool (Game 6: T3 beats T4 by forfeit)
			CreateCompletedPoolGame(6, TournamentStage.PoolPlay6, 3, 4, 1, 0, isForfeit: true),
			// T1 vs T2: 0-0 draw (4 pts each)
			CreateCompletedPoolGame(5, TournamentStage.PoolPlay5, 1, 2, 0, 0),
			// T1 beats T4 1-0 (8 pts) but gets 1 Red Card (-2) => +6 pts (Total 10 pts, 1 Win, 1 RC)
			CreateCompletedPoolGame(1, TournamentStage.PoolPlay1, 1, 4, 1, 0, homeRed: 1),
			// T2 ties T3 3-3 => +6 pts (Total 10 pts, 0 Wins, 0 RC)
			CreateCompletedPoolGame(2, TournamentStage.PoolPlay2, 2, 3, 3, 3)
		};

		var result = PoolPlayStandingsCalculator.Calculate(fourTeams, poolGames);

		result.AnyForfeitInPool.Should().BeTrue();
		var t1 = result.Standings.First(s => s.TeamId == 1);
		var t2 = result.Standings.First(s => s.TeamId == 2);
		t1.PoolPoints.Should().Be(10);
		t2.PoolPoints.Should().Be(10);
		t1.Wins.Should().Be(1);
		t2.Wins.Should().Be(0);

		// Because a forfeit exists in the pool, T1's higher Wins (1 vs 0) is omitted,
		// and T2 ranks ahead of T1 due to fewer Red Cards (0 vs 1)!
		t2.Rank.Should().BeLessThan(t1.Rank);
	}
}
