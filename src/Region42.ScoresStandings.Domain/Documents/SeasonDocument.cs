namespace Region42.ScoresStandings.Domain.Documents;

public class SeasonDocument
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public int Version { get; set; } = 1;
	public DateTime ModifiedAt { get; set; }
	public string ModifiedBy { get; set; } = string.Empty;
	public SeasonInfoDocument Season { get; set; } = new();
	public SettingsDocument Settings { get; set; } = new();
	public List<DivisionDocument> Divisions { get; set; } = new();
}

public class SeasonInfoDocument
{
	public int Id { get; set; }
	public int Year { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public string? CustomMessage { get; set; }
}

public class SettingsDocument
{
	public int MinVolunteerPointsForPlayoff { get; set; }
	public int DefaultPlayoffSpots { get; set; } = 1;
}

public class DivisionDocument
{
	public int Id { get; set; }
	public string Key { get; set; } = string.Empty;
	public string AgeGroup { get; set; } = string.Empty;
	public string Gender { get; set; } = string.Empty;
	public int TotalRounds { get; set; }
	public int PlayoffSpots { get; set; } = 1;
	public int ScrimmageRounds { get; set; }
	public string? CustomMessage { get; set; }
	public List<TeamDocument> Teams { get; set; } = new();
}

public class TeamDocument
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string ShortName { get; set; } = string.Empty;
	public string ContactName { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public bool IsRegion42Team { get; set; } = true;
}
