using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Pure in-memory standings calculation used for both read fallback and write-time persistence.
/// </summary>
public static class StandingsCalculator
{
	public static StandingsResult Calculate(
		Division division,
		IReadOnlyList<Team> teams,
		IReadOnlyList<Game> games,
		IReadOnlyList<Score> scores,
		IReadOnlyList<VolunteerPoints> volunteerPoints,
		int minVolunteerPointsForPlayoff,
		int throughRound,
		decimal maxVolunteerPointsPerSeason = 0m)
	{
		var activeTeams = teams
			.Where(t => t.DivisionId == division.Id && t.IsActive && t.IsRegion42Team)
			.ToList();

		if (!activeTeams.Any())
		{
			return new StandingsResult
			{
				DivisionId = division.Id,
				DivisionName = GetDivisionName(division),
				ThroughRound = throughRound,
				CalculatedAt = DateTime.UtcNow,
				Standings = new List<TeamStanding>(),
				ScrimmageRounds = division.ScrimmageRounds,
				ScrimmageRoundsInRange = throughRound == 0
					? Math.Min(division.ScrimmageRounds, division.TotalRounds)
					: Math.Min(division.ScrimmageRounds, throughRound)
			};
		}

		var hasTournament = division.EndOfSeasonTournamentFormat != TournamentFormatType.None;
		var completedRegularGames = games
			.Where(g =>
				g.DivisionId == division.Id &&
				g.Round <= throughRound &&
				g.Status == GameStatus.Completed &&
				g is not TournamentGame &&
				!(hasTournament && g.Round == division.TotalRounds))
			.ToList();

		var gameIds = completedRegularGames.Select(g => g.Id).ToHashSet();
		var relevantScores = scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = activeTeams.Select(t => t.Id).ToHashSet();
		var relevantVolunteerPoints = volunteerPoints
			.Where(vp => teamIds.Contains(vp.TeamId) && vp.Round <= throughRound)
			.ToList();

		var unsortedStandings = activeTeams
			.Select(team => CalculateTeamStanding(team, completedRegularGames, relevantScores, relevantVolunteerPoints, division.ScrimmageRounds, maxVolunteerPointsPerSeason))
			.ToList();

		var nonScrimmageGames = completedRegularGames
			.Where(g => g.Round > division.ScrimmageRounds)
			.ToList();

		var standings = SortStandingsWithTiebreakers(unsortedStandings, nonScrimmageGames, relevantScores);

		for (var i = 0; i < standings.Count; i++)
		{
			standings[i].Rank = i + 1;
		}

		var gamesPlayedCounts = standings.Select(s => s.GamesPlayed).Distinct().ToList();
		if (gamesPlayedCounts.Count > 1)
		{
			foreach (var standing in standings)
			{
				standing.PointsPerGame = standing.GamesPlayed > 0
					? Math.Round((decimal)standing.TotalPoints / standing.GamesPlayed, 2)
					: 0;
			}
		}

		var (isFinalStandings, tournamentWinnerTeamId) = DetermineFinalStandingsAndTournamentWinner(
			division,
			games,
			scores,
			throughRound);

		ApplyPlayoffQualification(
			standings,
			division.PlayoffSpots,
			minVolunteerPointsForPlayoff,
			tournamentWinnerTeamId);

		return new StandingsResult
		{
			DivisionId = division.Id,
			DivisionName = GetDivisionName(division),
			ThroughRound = throughRound,
			CalculatedAt = DateTime.UtcNow,
			Standings = standings,
			ScrimmageRounds = division.ScrimmageRounds,
			ScrimmageRoundsInRange = throughRound == 0
				? Math.Min(division.ScrimmageRounds, division.TotalRounds)
				: Math.Min(division.ScrimmageRounds, throughRound),
			IsFinalStandings = isFinalStandings,
			TournamentWinnerTeamId = tournamentWinnerTeamId
		};
	}

	public static Dictionary<string, Domain.Documents.StandingsSnapshotDocument> CalculateAllRounds(
		Division division,
		IReadOnlyList<Team> teams,
		IReadOnlyList<Game> games,
		IReadOnlyList<Score> scores,
		IReadOnlyList<VolunteerPoints> volunteerPoints,
		int minVolunteerPointsForPlayoff,
		decimal maxVolunteerPointsPerSeason = 0m)
	{
		var standingsByRound = new Dictionary<string, Domain.Documents.StandingsSnapshotDocument>();
		for (var round = 0; round <= division.TotalRounds; round++)
		{
			var result = Calculate(division, teams, games, scores, volunteerPoints, minVolunteerPointsForPlayoff, round, maxVolunteerPointsPerSeason);
			standingsByRound[round.ToString()] = Helpers.StandingsDocumentMapper.ToDocument(result);
		}

		return standingsByRound;
	}

	private static List<TeamStanding> SortStandingsWithTiebreakers(
		List<TeamStanding> standings,
		List<Game> nonScrimmageGames,
		List<Score> scores)
	{
		var sorted = new List<TeamStanding>(standings.Count);
		var scoresByGameId = scores
			.GroupBy(s => s.GameId)
			.ToDictionary(g => g.Key, g => g.First());

		var groups = standings
			.GroupBy(s => (s.TotalPoints, s.GamePoints))
			.OrderByDescending(g => g.Key.TotalPoints)
			.ThenByDescending(g => g.Key.GamePoints);

		foreach (var group in groups)
		{
			var groupTeams = group.ToList();
			if (groupTeams.Count == 1)
			{
				sorted.Add(groupTeams[0]);
				continue;
			}

			var tiedTeamIds = groupTeams.Select(t => t.TeamId).ToHashSet();
			var headToHeadPoints = CalculateHeadToHeadPoints(tiedTeamIds, nonScrimmageGames, scoresByGameId);

			var orderedGroup = groupTeams
				.OrderByDescending(s => headToHeadPoints.GetValueOrDefault(s.TeamId, 0))
				.ThenByDescending(s => s.GoalDifferential)
				.ThenByDescending(s => s.GoalsFor)
				.ThenBy(s => s.TeamName)
				.ToList();

			sorted.AddRange(orderedGroup);
		}

		return sorted;
	}

	private static Dictionary<int, int> CalculateHeadToHeadPoints(
		HashSet<int> tiedTeamIds,
		List<Game> nonScrimmageGames,
		Dictionary<int, Score> scoresByGameId)
	{
		var points = tiedTeamIds.ToDictionary(id => id, _ => 0);

		foreach (var game in nonScrimmageGames)
		{
			if (!tiedTeamIds.Contains(game.HomeTeamId) || !tiedTeamIds.Contains(game.AwayTeamId))
			{
				continue;
			}

			var score = game.Score ?? scoresByGameId.GetValueOrDefault(game.Id);
			if (score is not { HomeScore: { } homeScore, AwayScore: { } awayScore })
			{
				continue;
			}

			if (homeScore > awayScore)
			{
				points[game.HomeTeamId] += 3;
			}
			else if (awayScore > homeScore)
			{
				points[game.AwayTeamId] += 3;
			}
			else
			{
				points[game.HomeTeamId] += 1;
				points[game.AwayTeamId] += 1;
			}
		}

		return points;
	}

	private static (bool IsFinalStandings, int? TournamentWinnerTeamId) DetermineFinalStandingsAndTournamentWinner(
		Division division,
		IReadOnlyList<Game> games,
		IReadOnlyList<Score> scores,
		int throughRound)
	{
		if (throughRound < division.TotalRounds)
		{
			return (false, null);
		}

		if (division.EndOfSeasonTournamentFormat != TournamentFormatType.None)
		{
			var finalGame = games
				.OfType<EliminationGame>()
				.FirstOrDefault(g => g.DivisionId == division.Id && g.Stage == TournamentStage.Final);

			if (finalGame == null)
			{
				return (false, null);
			}

			finalGame.Score ??= scores.FirstOrDefault(s => s.GameId == finalGame.Id);
			var winnerTeamId = finalGame.TryGetWinnerTeamId();
			return (winnerTeamId.HasValue, winnerTeamId);
		}

		var finalRoundRegularGames = games
			.Where(g => g.DivisionId == division.Id && g.Round == division.TotalRounds && g is not TournamentGame)
			.ToList();

		var isRegularSeasonComplete = finalRoundRegularGames.Count > 0 &&
			finalRoundRegularGames.All(g => g.Status is GameStatus.Completed or GameStatus.Cancelled);

		return (isRegularSeasonComplete, null);
	}

	private static TeamStanding CalculateTeamStanding(
		Team team,
		List<Game> games,
		List<Score> scores,
		List<VolunteerPoints> volunteerPoints,
		int scrimmageRounds,
		decimal maxVolunteerPointsPerSeason)
	{
		var standing = new TeamStanding
		{
			TeamId = team.Id,
			TeamName = team.Name,
			TeamShortName = team.ShortName
		};

		var teamGames = games
			.Where(g => (g.HomeTeamId == team.Id || g.AwayTeamId == team.Id) && g.Round > scrimmageRounds)
			.ToList();
		standing.GamesPlayed = teamGames.Count;

		foreach (var game in teamGames)
		{
			var score = scores.FirstOrDefault(s => s.GameId == game.Id);
			if (score == null || !score.HomeScore.HasValue || !score.AwayScore.HasValue)
			{
				continue;
			}

			var isHomeTeam = game.HomeTeamId == team.Id;
			var goalsFor = isHomeTeam ? score.HomeScore.Value : score.AwayScore.Value;
			var goalsAgainst = isHomeTeam ? score.AwayScore.Value : score.HomeScore.Value;

			standing.GoalsFor += goalsFor;
			standing.GoalsAgainst += goalsAgainst;

			if (goalsFor > goalsAgainst)
			{
				standing.Wins++;
				standing.GamePoints += 3;
			}
			else if (goalsFor == goalsAgainst)
			{
				standing.Draws++;
				standing.GamePoints += 1;
			}
			else
			{
				standing.Losses++;
			}
		}

		standing.GoalDifferential = standing.GoalsFor - standing.GoalsAgainst;
		var totalVolunteerPoints = volunteerPoints.Where(vp => vp.TeamId == team.Id).Sum(vp => vp.Points);
		if (maxVolunteerPointsPerSeason > 0m && totalVolunteerPoints > maxVolunteerPointsPerSeason)
		{
			totalVolunteerPoints = maxVolunteerPointsPerSeason;
		}

		standing.VolunteerPoints = totalVolunteerPoints;
		standing.TotalPoints = standing.GamePoints + standing.VolunteerPoints;

		return standing;
	}

	private static string GetDivisionName(Division division)
	{
		var ageGroup = division.AgeGroup switch
		{
			AgeGroup.U10 => "10U",
			AgeGroup.U12 => "12U",
			AgeGroup.U14 => "14U",
			_ => division.AgeGroup.ToString()
		};

		var gender = division.Gender == Gender.Boys ? "Boys" : "Girls";
		return $"{ageGroup} {gender}";
	}

	private static void ApplyPlayoffQualification(
		List<TeamStanding> standings,
		int playoffSpots,
		int minVolunteerPoints,
		int? tournamentWinnerTeamId)
	{
		if (tournamentWinnerTeamId.HasValue)
		{
			ApplyTournamentPlayoffQualification(standings, playoffSpots, minVolunteerPoints, tournamentWinnerTeamId.Value);
			return;
		}

		foreach (var standing in standings)
		{
			var hasMinVolunteerPoints = standing.VolunteerPoints >= minVolunteerPoints;
			var withinPlayoffSpots = standing.Rank <= playoffSpots;
			standing.QualifiesForPlayoffs = hasMinVolunteerPoints && withinPlayoffSpots;

			if (standing.QualifiesForPlayoffs)
			{
				standing.PlayoffQualificationNote = "Clinched playoff spot";
			}
			else if (!hasMinVolunteerPoints && withinPlayoffSpots)
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = FormatVolunteerPointsNeeded(needed, "to qualify");
			}
			else if (hasMinVolunteerPoints && !withinPlayoffSpots)
			{
				standing.PlayoffQualificationNote = "Eliminated from playoffs";
			}
			else
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = FormatVolunteerPointsNeeded(needed, "and must improve standing");
			}
		}
	}

	private static void ApplyTournamentPlayoffQualification(
		List<TeamStanding> standings,
		int playoffSpots,
		int minVolunteerPoints,
		int tournamentWinnerTeamId)
	{
		var eligibleStandings = standings
			.Where(s => s.VolunteerPoints >= minVolunteerPoints)
			.ToList();

		var qualifyingTeamIds = new HashSet<int>();

		if (playoffSpots == 1)
		{
			if (eligibleStandings.Count > 0)
			{
				qualifyingTeamIds.Add(eligibleStandings[0].TeamId);
			}
		}
		else if (playoffSpots >= 2)
		{
			var standingsBerths = playoffSpots - 1;
			foreach (var standing in eligibleStandings.Take(standingsBerths))
			{
				qualifyingTeamIds.Add(standing.TeamId);
			}

			var winnerStanding = eligibleStandings.FirstOrDefault(s => s.TeamId == tournamentWinnerTeamId);
			if (winnerStanding != null && !qualifyingTeamIds.Contains(winnerStanding.TeamId))
			{
				qualifyingTeamIds.Add(winnerStanding.TeamId);
			}
			else
			{
				var nextEligible = eligibleStandings.FirstOrDefault(s => !qualifyingTeamIds.Contains(s.TeamId));
				if (nextEligible != null)
				{
					qualifyingTeamIds.Add(nextEligible.TeamId);
				}
			}

			foreach (var standing in eligibleStandings)
			{
				if (qualifyingTeamIds.Count >= playoffSpots)
				{
					break;
				}

				qualifyingTeamIds.Add(standing.TeamId);
			}
		}

		foreach (var standing in standings)
		{
			var hasMinVolunteerPoints = standing.VolunteerPoints >= minVolunteerPoints;
			var isTournamentWinner = standing.TeamId == tournamentWinnerTeamId;
			var withinPlayoffPosition = standing.Rank <= playoffSpots || (playoffSpots >= 2 && isTournamentWinner);
			standing.QualifiesForPlayoffs = qualifyingTeamIds.Contains(standing.TeamId);

			if (standing.QualifiesForPlayoffs)
			{
				standing.PlayoffQualificationNote = isTournamentWinner
					? "Tournament Champion - Clinched playoff spot"
					: "Clinched playoff spot";
			}
			else if (!hasMinVolunteerPoints && withinPlayoffPosition)
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = FormatVolunteerPointsNeeded(needed, "to qualify");
			}
			else if (hasMinVolunteerPoints)
			{
				standing.PlayoffQualificationNote = "Eliminated from playoffs";
			}
			else
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = FormatVolunteerPointsNeeded(needed, "and must improve standing");
			}
		}
	}

	private static string FormatVolunteerPointsNeeded(decimal needed, string suffix)
	{
		var formatted = needed == 1m ? "1 more referee point" : $"{needed:G29} more referee points";
		return $"Needs {formatted} {suffix}";
	}
}
