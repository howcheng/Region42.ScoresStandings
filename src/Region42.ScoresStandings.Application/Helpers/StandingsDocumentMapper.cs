using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Helpers;

public static class StandingsDocumentMapper
{
	public static StandingsSnapshotDocument ToDocument(StandingsResult result)
	{
		return new StandingsSnapshotDocument
		{
			ThroughRound = result.ThroughRound,
			CalculatedAt = result.CalculatedAt,
			ScrimmageRounds = result.ScrimmageRounds,
			ScrimmageRoundsInRange = result.ScrimmageRoundsInRange,
			Teams = result.Standings.Select(ToDocument).ToList()
		};
	}

	public static StandingsResult ToEntity(
		StandingsSnapshotDocument doc,
		int divisionId,
		string divisionName,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		return new StandingsResult
		{
			DivisionId = divisionId,
			DivisionName = divisionName,
			ThroughRound = doc.ThroughRound,
			CalculatedAt = doc.CalculatedAt,
			ScrimmageRounds = doc.ScrimmageRounds,
			ScrimmageRoundsInRange = doc.ScrimmageRoundsInRange,
			Standings = doc.Teams.Select(teamDoc => ToEntity(teamDoc, teamsById)).ToList()
		};
	}

	public static TeamStandingDocument ToDocument(TeamStanding standing)
	{
		return new TeamStandingDocument
		{
			Rank = standing.Rank,
			TeamId = standing.TeamId,
			GamesPlayed = standing.GamesPlayed,
			Wins = standing.Wins,
			Draws = standing.Draws,
			Losses = standing.Losses,
			GoalsFor = standing.GoalsFor,
			GoalsAgainst = standing.GoalsAgainst,
			GoalDifferential = standing.GoalDifferential,
			GamePoints = standing.GamePoints,
			VolunteerPoints = standing.VolunteerPoints,
			TotalPoints = standing.TotalPoints,
			PointsPerGame = standing.PointsPerGame,
			QualifiesForPlayoffs = standing.QualifiesForPlayoffs,
			PlayoffQualificationNote = standing.PlayoffQualificationNote
		};
	}

	public static TeamStanding ToEntity(TeamStandingDocument doc, IReadOnlyDictionary<int, Team> teamsById)
	{
		teamsById.TryGetValue(doc.TeamId, out var team);

		return new TeamStanding
		{
			Rank = doc.Rank,
			TeamId = doc.TeamId,
			TeamName = team?.Name ?? "Unknown",
			TeamShortName = team?.ShortName ?? "Unknown",
			GamesPlayed = doc.GamesPlayed,
			Wins = doc.Wins,
			Draws = doc.Draws,
			Losses = doc.Losses,
			GoalsFor = doc.GoalsFor,
			GoalsAgainst = doc.GoalsAgainst,
			GoalDifferential = doc.GoalDifferential,
			GamePoints = doc.GamePoints,
			VolunteerPoints = doc.VolunteerPoints,
			TotalPoints = doc.TotalPoints,
			PointsPerGame = doc.PointsPerGame,
			QualifiesForPlayoffs = doc.QualifiesForPlayoffs,
			PlayoffQualificationNote = doc.PlayoffQualificationNote
		};
	}
}
