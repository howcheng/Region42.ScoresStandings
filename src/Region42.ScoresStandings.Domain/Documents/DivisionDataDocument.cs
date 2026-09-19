namespace Region42.ScoresStandings.Domain.Documents;

public class DivisionDataDocument
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public int Version { get; set; } = 1;
	public string DivisionKey { get; set; } = string.Empty;
	public int DivisionId { get; set; }
	public DateTime ModifiedAt { get; set; }
	public string ModifiedBy { get; set; } = string.Empty;
	public List<GameDocument> Games { get; set; } = new();
	public List<VolunteerPointsDocument> VolunteerPoints { get; set; } = new();
	public Dictionary<string, StandingsSnapshotDocument> StandingsByRound { get; set; } = new();
}

public class GameDocument
{
	public int Id { get; set; }
	public int Round { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public DateTime ModifiedAt { get; set; }
	public string ModifiedBy { get; set; } = string.Empty;
}

public class VolunteerPointsDocument
{
	public int TeamId { get; set; }
	public int Round { get; set; }
	public int Points { get; set; }
	public string Notes { get; set; } = string.Empty;
	public DateTime ModifiedAt { get; set; }
	public string ModifiedBy { get; set; } = string.Empty;
}

public class StandingsSnapshotDocument
{
	public int ThroughRound { get; set; }
	public DateTime CalculatedAt { get; set; }
	public int ScrimmageRounds { get; set; }
	public int ScrimmageRoundsInRange { get; set; }
	public List<TeamStandingDocument> Teams { get; set; } = new();
}

public class TeamStandingDocument
{
	public int Rank { get; set; }
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public string TeamShortName { get; set; } = string.Empty;
	public int GamesPlayed { get; set; }
	public int Wins { get; set; }
	public int Draws { get; set; }
	public int Losses { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
	public int GoalDifferential { get; set; }
	public int GamePoints { get; set; }
	public int VolunteerPoints { get; set; }
	public int TotalPoints { get; set; }
	public decimal PointsPerGame { get; set; }
	public bool QualifiesForPlayoffs { get; set; }
	public string? PlayoffQualificationNote { get; set; }
}
