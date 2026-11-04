using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Application.Services.Tournaments;
using Region42.ScoresStandings.Application.Tests.Helpers;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Tests.Services;

public class TournamentServiceTests
{
	private readonly TestCompetitionDataContext _context;
	private readonly TournamentBracketBuilderFactory _factory;
	private readonly Mock<ILogger<TournamentService>> _mockLogger;
	private readonly TournamentService _service;

	public TournamentServiceTests()
	{
		_context = new TestCompetitionDataContext();
		_factory = new TournamentBracketBuilderFactory(new ITournamentBracketBuilder[] { new SixTeamBracketBuilder() });
		_mockLogger = new Mock<ILogger<TournamentService>>();
		_service = new TournamentService(_context, _factory, _mockLogger.Object);
	}

	[Fact]
	public void Factory_GetBuilder_With6Teams_ReturnsSixTeamBracketBuilder()
	{
		var builder = _factory.GetBuilder(6);
		builder.Should().BeOfType<SixTeamBracketBuilder>();
		builder.TeamCount.Should().Be(6);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(9)]
	public void Factory_GetBuilder_WithOutOfRangeTeamCount_ThrowsArgumentOutOfRangeException(int teamCount)
	{
		var act = () => _factory.GetBuilder(teamCount);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(7)]
	[InlineData(8)]
	public void Factory_GetBuilder_WithUnimplementedTeamCount_ThrowsNotSupportedException(int teamCount)
	{
		var act = () => _factory.GetBuilder(teamCount);
		act.Should().Throw<NotSupportedException>();
	}

	[Fact]
	public async Task EnsureTournamentGamesAsync_For6TeamBracket_GeneratesMatchupsAndAdvancesWinnersThroughFinal()
	{
		var division = TestDataBuilder.CreateDivision(id: 1, ageGroup: AgeGroup.U12, gender: Gender.Boys, totalRounds: 10);
		division.EndOfSeasonTournamentFormat = TournamentFormatType.Bracket;
		_context.Divisions.Add(division);

		// Create 6 teams and give them distinct volunteer points in Round 9 so seeds 1..6 are deterministic (Team 1 = Seed 1, ..., Team 6 = Seed 6)
		for (var i = 1; i <= 6; i++)
		{
			var team = TestDataBuilder.CreateTeam(id: i, divisionId: 1, name: $"12UB0{i} Team {i}");
			_context.Teams.Add(team);
			_context.VolunteerPoints.Add(TestDataBuilder.CreateVolunteerPoints(id: i, teamId: i, round: 9, points: 10 - i));
		}

		// Complete Round 9 regular game so Round 9 is considered complete
		var r9Game = TestDataBuilder.CreateGame(id: 100, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 9, status: GameStatus.Completed);
		var r9Score = TestDataBuilder.CreateScore(id: 100, gameId: 100, homeScore: 1, awayScore: 1);
		r9Game.Score = r9Score;
		_context.Games.Add(r9Game);
		_context.Scores.Add(r9Score);

		var generated = await _service.EnsureTournamentGamesAsync(1);
		generated.Should().BeTrue();

		var bracketGames = _context.Games.OfType<EliminationGame>().Where(g => g.DivisionId == 1).ToList();
		bracketGames.Should().HaveCount(5);

		var qf1 = bracketGames.Single(g => g.Stage == TournamentStage.Quarterfinal1);
		var qf2 = bracketGames.Single(g => g.Stage == TournamentStage.Quarterfinal2);
		var sf1 = bracketGames.Single(g => g.Stage == TournamentStage.Semifinal1);
		var sf2 = bracketGames.Single(g => g.Stage == TournamentStage.Semifinal2);
		var finalGame = bracketGames.Single(g => g.Stage == TournamentStage.Final);

		// QF1 is 4th vs 5th; QF2 is 3rd vs 6th; SF1 has 1st seed waiting; SF2 has 2nd seed waiting
		qf1.HomeTeamId.Should().Be(4);
		qf1.AwayTeamId.Should().Be(5);
		qf1.Status.Should().Be(GameStatus.Unscheduled);
		qf1.ScheduledDateTime.Should().Be(default);
		qf1.Location.Should().BeEmpty();
		qf2.HomeTeamId.Should().Be(3);
		qf2.AwayTeamId.Should().Be(6);
		sf1.HomeTeamId.Should().Be(1);
		sf1.AwayTeamId.Should().Be(0);
		sf2.HomeTeamId.Should().Be(2);
		sf2.AwayTeamId.Should().Be(0);

		// Enter QF1 score: 5th seed wins on penalty shootout after 2-2 tie (4-3 PK)
		qf1.Status = GameStatus.Completed;
		var qf1Score = new EliminationScore
		{
			Id = qf1.Id,
			GameId = qf1.Id,
			Game = qf1,
			HomeScore = 2,
			AwayScore = 2,
			IsDecidedByPenalties = true,
			HomePenaltyScore = 3,
			AwayPenaltyScore = 4
		};
		qf1.Score = qf1Score;
		_context.Scores.Add(qf1Score);

		// Enter QF2 score: 3rd seed wins 2-0 in regulation
		qf2.Status = GameStatus.Completed;
		var qf2Score = new EliminationScore
		{
			Id = qf2.Id,
			GameId = qf2.Id,
			Game = qf2,
			HomeScore = 2,
			AwayScore = 0
		};
		qf2.Score = qf2Score;
		_context.Scores.Add(qf2Score);

		await _service.SyncTournamentProgressionAsync(1);

		// 5th seed advances to play 1st seed in SF1; 3rd seed advances to play 2nd seed in SF2
		sf1.AwayTeamId.Should().Be(5);
		sf2.AwayTeamId.Should().Be(3);

		// Complete Semifinals: Seed 1 beats Seed 5 (1-0), Seed 3 beats Seed 2 (2-1)
		sf1.Status = GameStatus.Completed;
		sf1.Score = new EliminationScore { Id = sf1.Id, GameId = sf1.Id, Game = sf1, HomeScore = 1, AwayScore = 0 };
		_context.Scores.Add(sf1.Score);

		sf2.Status = GameStatus.Completed;
		sf2.Score = new EliminationScore { Id = sf2.Id, GameId = sf2.Id, Game = sf2, HomeScore = 1, AwayScore = 2 };
		_context.Scores.Add(sf2.Score);

		await _service.SyncTournamentProgressionAsync(1);

		finalGame.HomeTeamId.Should().Be(1);
		finalGame.AwayTeamId.Should().Be(3);

		// Complete Final: Seed 3 wins 2-1
		finalGame.Status = GameStatus.Completed;
		finalGame.Score = new EliminationScore { Id = finalGame.Id, GameId = finalGame.Id, Game = finalGame, HomeScore = 1, AwayScore = 2 };
		_context.Scores.Add(finalGame.Score);

		var display = await _service.GetTournamentBracketAsync(1);
		display.Should().NotBeNull();
		display!.IsCompleted.Should().BeTrue();
		display.ChampionTeamId.Should().Be(3);
	}

	[Theory]
	[InlineData(3, 3)]
	[InlineData(4, 6)]
	public async Task GenerateOrResetTournamentGamesAsync_ForPoolPlayWithFinal_Supports3And4TeamPools(int teamCount, int expectedPoolGameCount)
	{
		var division = TestDataBuilder.CreateDivision(id: 2, ageGroup: AgeGroup.U14, gender: Gender.Boys, totalRounds: 10);
		division.EndOfSeasonTournamentFormat = TournamentFormatType.PoolPlayWithFinal;
		_context.Divisions.Add(division);

		for (var i = 1; i <= teamCount; i++)
		{
			var team = TestDataBuilder.CreateTeam(id: i, divisionId: 2, name: $"14UB0{i} Team {i}");
			_context.Teams.Add(team);
			_context.VolunteerPoints.Add(TestDataBuilder.CreateVolunteerPoints(id: i, teamId: i, round: 1, points: 10 - i));
		}

		// On-demand generation before Round 9 is complete
		var generated = await _service.GenerateOrResetTournamentGamesAsync(2);
		generated.Should().BeTrue();

		var poolGames = _context.Games.OfType<PoolPlayGame>().Where(g => g.DivisionId == 2).ToList();
		var finalGames = _context.Games.OfType<EliminationGame>().Where(g => g.DivisionId == 2).ToList();

		poolGames.Should().HaveCount(expectedPoolGameCount);
		finalGames.Should().ContainSingle(g => g.Stage == TournamentStage.Final);
	}
}
