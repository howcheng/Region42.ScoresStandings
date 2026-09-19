using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Documents;

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

	public static StandingsResult ToEntity(StandingsSnapshotDocument doc, int divisionId, string divisionName)
	{
		return new StandingsResult
		{
			DivisionId = divisionId,
			DivisionName = divisionName,
			ThroughRound = doc.ThroughRound,
			CalculatedAt = doc.CalculatedAt,
			ScrimmageRounds = doc.ScrimmageRounds,
			ScrimmageRoundsInRange = doc.ScrimmageRoundsInRange,
			Standings = doc.Teams.Select(ToEntity).ToList()
		};
	}

	public static TeamStandingDocument ToDocument(TeamStanding standing)
	{
		return new TeamStandingDocument
		{
			Rank = standing.Rank,
			TeamId = standing.TeamId,
			TeamName = standing.TeamName,
			TeamShortName = standing.TeamShortName,
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

	public static TeamStanding ToEntity(TeamStandingDocument doc)
	{
		return new TeamStanding
		{
			Rank = doc.Rank,
			TeamId = doc.TeamId,
			TeamName = doc.TeamName,
			TeamShortName = doc.TeamShortName,
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
