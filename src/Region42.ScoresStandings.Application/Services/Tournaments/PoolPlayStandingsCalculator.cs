using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Services.Tournaments;

/// <summary>
/// Calculates Pool Play standings using the AYSO 10-point system and tiebreaker rules:
/// - Win = 6 pts, Tie = 3 pts, Loss = 0 pts
/// - Goal = 1 pt per goal up to max 3 per game (win, lose, or draw)
/// - Earned Shutout = 1 pt (when goals allowed == 0; not awarded on forfeits)
/// - Forfeit = 7 pts (scored as 1-0 win, no shutout point)
/// - Red Card = -2 pts per card
/// - Yellow Card = -1 pt per card
/// - Volunteer points are not added.
/// </summary>
public static class PoolPlayStandingsCalculator
{
	public const int WinPoints = 6;
	public const int TiePoints = 3;
	public const int LossPoints = 0;
	public const int MaxGoalBonusPerGame = 3;
	public const int ShutoutBonusPoints = 1;
	public const int ForfeitWinPoints = 7;
	public const int RedCardDeduction = 2;
	public const int YellowCardDeduction = 1;
	public const int MaxGoalsAllowedPerGameForTiebreaker = 5;

	public static PoolPlayStandingsResult Calculate(
		IReadOnlyList<TeamStanding> seededTeams,
		IReadOnlyList<PoolPlayGame> poolGames,
		int? poolPlayTiebreakerWinnerTeamId = null)
	{
		ArgumentNullException.ThrowIfNull(seededTeams);
		ArgumentNullException.ThrowIfNull(poolGames);

		var completedPoolGames = poolGames
			.Where(g => g.Status == GameStatus.Completed && g.Score is { HomeScore: not null, AwayScore: not null })
			.ToList();

		var anyForfeitInPool = completedPoolGames
			.Any(g => g.TournamentScore?.IsForfeit == true);

		var expectedGameCount = seededTeams.Count * (seededTeams.Count - 1) / 2;
		var allPoolGamesCompleted = expectedGameCount > 0 && completedPoolGames.Count >= expectedGameCount;

		var rows = seededTeams
			.Select(seedTeam => CalculateTeamPoolStanding(seedTeam, completedPoolGames))
			.ToList();

		var (sortedRows, requiresPkTiebreaker, tiedTeamIds) = SortWithTiebreakers(
			rows,
			completedPoolGames,
			anyForfeitInPool,
			allPoolGamesCompleted,
			poolPlayTiebreakerWinnerTeamId);

		for (var i = 0; i < sortedRows.Count; i++)
		{
			sortedRows[i].Rank = i + 1;
			sortedRows[i].AdvancesToFinal = i < 2;
		}

		return new PoolPlayStandingsResult
		{
			Standings = sortedRows,
			AnyForfeitInPool = anyForfeitInPool,
			AllPoolGamesCompleted = allPoolGamesCompleted,
			RequiresPenaltyShootoutTiebreaker = requiresPkTiebreaker,
			TiedTeamIdsForTiebreaker = tiedTeamIds
		};
	}

	private static PoolPlayStandingDto CalculateTeamPoolStanding(
		TeamStanding seededTeam,
		List<PoolPlayGame> completedPoolGames)
	{
		var dto = new PoolPlayStandingDto
		{
			Seed = seededTeam.Rank,
			TeamId = seededTeam.TeamId,
			TeamName = seededTeam.TeamName,
			TeamShortName = seededTeam.TeamShortName
		};

		var teamGames = completedPoolGames
			.Where(g => g.HomeTeamId == seededTeam.TeamId || g.AwayTeamId == seededTeam.TeamId)
			.ToList();

		dto.GamesPlayed = teamGames.Count;

		foreach (var game in teamGames)
		{
			var score = game.Score!;
			var isHome = game.HomeTeamId == seededTeam.TeamId;
			var goalsFor = isHome ? score.HomeScore!.Value : score.AwayScore!.Value;
			var goalsAgainst = isHome ? score.AwayScore!.Value : score.HomeScore!.Value;

			var poolScore = game.PoolPlayScore;
			var isForfeit = game.TournamentScore?.IsForfeit == true;
			var redCards = isHome ? (poolScore?.HomeRedCards ?? 0) : (poolScore?.AwayRedCards ?? 0);
			var yellowCards = isHome ? (poolScore?.HomeYellowCards ?? 0) : (poolScore?.AwayYellowCards ?? 0);

			dto.GoalsFor += goalsFor;
			dto.GoalsAgainst += goalsAgainst;
			dto.CappedGoalsAllowed += Math.Min(goalsAgainst, MaxGoalsAllowedPerGameForTiebreaker);
			dto.CappedGoalDifferential += Math.Min(goalsFor, MaxGoalBonusPerGame) - goalsAgainst;
			dto.RedCards += redCards;
			dto.YellowCards += yellowCards;

			if (isForfeit)
			{
				if (goalsFor > goalsAgainst)
				{
					dto.Wins++;
					dto.PoolPoints += ForfeitWinPoints;
				}
				else
				{
					dto.Losses++;
				}
			}
			else
			{
				if (goalsFor > goalsAgainst)
				{
					dto.Wins++;
					dto.PoolPoints += WinPoints;
				}
				else if (goalsFor == goalsAgainst)
				{
					dto.Draws++;
					dto.PoolPoints += TiePoints;
				}
				else
				{
					dto.Losses++;
					dto.PoolPoints += LossPoints;
				}

				dto.PoolPoints += Math.Min(goalsFor, MaxGoalBonusPerGame);

				if (goalsAgainst == 0)
				{
					dto.PoolPoints += ShutoutBonusPoints;
				}
			}

			dto.PoolPoints -= (redCards * RedCardDeduction) + (yellowCards * YellowCardDeduction);
		}

		dto.GoalDifferential = dto.GoalsFor - dto.GoalsAgainst;

		return dto;
	}

	private static (List<PoolPlayStandingDto> Sorted, bool RequiresPkTiebreaker, List<int> TiedTeamIds) SortWithTiebreakers(
		List<PoolPlayStandingDto> rows,
		List<PoolPlayGame> completedPoolGames,
		bool anyForfeitInPool,
		bool allPoolGamesCompleted,
		int? poolPlayTiebreakerWinnerTeamId)
	{
		var sorted = new List<PoolPlayStandingDto>(rows.Count);
		var statisticalSignatureByTeamId = new Dictionary<int, (int PoolPoints, int H2H, int EffectiveWins, int RedCards, int YellowCards, int CappedGA, int EffectiveCappedGD)>();

		var pointGroups = rows
			.GroupBy(r => r.PoolPoints)
			.OrderByDescending(g => g.Key);

		foreach (var group in pointGroups)
		{
			var groupTeams = group.ToList();
			if (groupTeams.Count == 1)
			{
				var single = groupTeams[0];
				statisticalSignatureByTeamId[single.TeamId] = (
					single.PoolPoints,
					0,
					anyForfeitInPool ? 0 : single.Wins,
					single.RedCards,
					single.YellowCards,
					single.CappedGoalsAllowed,
					anyForfeitInPool ? 0 : single.CappedGoalDifferential);
				sorted.Add(single);
				continue;
			}

			// Head-to-head competition is ONLY used in a 2-way tie (omitted in 3-way or 4-way ties).
			var h2hScoreByTeamId = groupTeams.ToDictionary(t => t.TeamId, _ => 0);
			if (groupTeams.Count == 2)
			{
				var teamAId = groupTeams[0].TeamId;
				var teamBId = groupTeams[1].TeamId;
				var h2hGame = completedPoolGames.FirstOrDefault(g =>
					(g.HomeTeamId == teamAId && g.AwayTeamId == teamBId) ||
					(g.HomeTeamId == teamBId && g.AwayTeamId == teamAId));

				if (h2hGame?.Score is { HomeScore: { } hScore, AwayScore: { } aScore })
				{
					if (hScore > aScore)
					{
						h2hScoreByTeamId[h2hGame.HomeTeamId] = 1;
					}
					else if (aScore > hScore)
					{
						h2hScoreByTeamId[h2hGame.AwayTeamId] = 1;
					}
				}
			}

			foreach (var team in groupTeams)
			{
				statisticalSignatureByTeamId[team.TeamId] = (
					team.PoolPoints,
					h2hScoreByTeamId[team.TeamId],
					anyForfeitInPool ? 0 : team.Wins,
					team.RedCards,
					team.YellowCards,
					team.CappedGoalsAllowed,
					anyForfeitInPool ? 0 : team.CappedGoalDifferential);
			}

			var orderedGroup = groupTeams
				.OrderByDescending(t => h2hScoreByTeamId[t.TeamId])
				.ThenByDescending(t => anyForfeitInPool ? 0 : t.Wins)
				.ThenBy(t => t.RedCards)
				.ThenBy(t => t.YellowCards)
				.ThenBy(t => t.CappedGoalsAllowed)
				.ThenByDescending(t => anyForfeitInPool ? 0 : t.CappedGoalDifferential)
				.ThenByDescending(t => poolPlayTiebreakerWinnerTeamId.HasValue && t.TeamId == poolPlayTiebreakerWinnerTeamId.Value ? 1 : 0)
				.ThenBy(t => t.Seed)
				.ThenBy(t => t.TeamName)
				.ToList();

			sorted.AddRange(orderedGroup);
		}

		var requiresPkTiebreaker = false;
		var tiedTeamIds = new List<int>();

		if (allPoolGamesCompleted && sorted.Count >= 2)
		{
			var signatureGroups = sorted
				.Select((team, index) => (Team: team, Rank: index + 1, Signature: statisticalSignatureByTeamId[team.TeamId]))
				.GroupBy(x => x.Signature)
				.Where(g => g.Count() > 1)
				.ToList();

			foreach (var sigGroup in signatureGroups)
			{
				var minRank = sigGroup.Min(x => x.Rank);
				var maxRank = sigGroup.Max(x => x.Rank);
				// A tie matters for advancing to the Final if it crosses the 2nd/3rd boundary (minRank <= 2 && maxRank >= 3)
				// or ties for 1st/2nd seed in the Final (minRank <= 2).
				if (minRank <= 2 && maxRank >= 2)
				{
					requiresPkTiebreaker = true;
					tiedTeamIds.AddRange(sigGroup.Select(x => x.Team.TeamId));
				}
			}
		}

		return (sorted, requiresPkTiebreaker, tiedTeamIds);
	}
}
