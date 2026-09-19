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
		int throughRound)
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

		var completedGames = games
			.Where(g => g.DivisionId == division.Id && g.Round <= throughRound && g.Status == GameStatus.Completed)
			.ToList();

		var gameIds = completedGames.Select(g => g.Id).ToHashSet();
		var relevantScores = scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = activeTeams.Select(t => t.Id).ToHashSet();
		var relevantVolunteerPoints = volunteerPoints
			.Where(vp => teamIds.Contains(vp.TeamId) && vp.Round <= throughRound)
			.ToList();

		var standings = activeTeams
			.Select(team => CalculateTeamStanding(team, completedGames, relevantScores, relevantVolunteerPoints, division.ScrimmageRounds))
			.OrderByDescending(s => s.TotalPoints)
			.ThenByDescending(s => s.GoalDifferential)
			.ThenByDescending(s => s.GoalsFor)
			.ThenBy(s => s.TeamName)
			.ToList();

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

		ApplyPlayoffQualification(standings, division.PlayoffSpots, minVolunteerPointsForPlayoff);

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
				: Math.Min(division.ScrimmageRounds, throughRound)
		};
	}

	public static Dictionary<string, Domain.Documents.StandingsSnapshotDocument> CalculateAllRounds(
		Division division,
		IReadOnlyList<Team> teams,
		IReadOnlyList<Game> games,
		IReadOnlyList<Score> scores,
		IReadOnlyList<VolunteerPoints> volunteerPoints,
		int minVolunteerPointsForPlayoff)
	{
		var standingsByRound = new Dictionary<string, Domain.Documents.StandingsSnapshotDocument>();
		for (var round = 0; round <= division.TotalRounds; round++)
		{
			var result = Calculate(division, teams, games, scores, volunteerPoints, minVolunteerPointsForPlayoff, round);
			standingsByRound[round.ToString()] = Helpers.StandingsDocumentMapper.ToDocument(result);
		}

		return standingsByRound;
	}

	private static TeamStanding CalculateTeamStanding(
		Team team,
		List<Game> games,
		List<Score> scores,
		List<VolunteerPoints> volunteerPoints,
		int scrimmageRounds)
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
		standing.VolunteerPoints = volunteerPoints.Where(vp => vp.TeamId == team.Id).Sum(vp => vp.Points);
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
		int minVolunteerPoints)
	{
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
				standing.PlayoffQualificationNote = needed == 1
					? "Needs 1 more volunteer point to qualify"
					: $"Needs {needed} more volunteer points to qualify";
			}
			else if (hasMinVolunteerPoints && !withinPlayoffSpots)
			{
				standing.PlayoffQualificationNote = "Eliminated from playoffs";
			}
			else
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = needed == 1
					? "Needs 1 more volunteer point and must improve standing"
					: $"Needs {needed} more volunteer points and must improve standing";
			}
		}
	}
}
