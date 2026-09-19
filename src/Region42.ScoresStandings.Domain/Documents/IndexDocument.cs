namespace Region42.ScoresStandings.Domain.Documents;

public class IndexDocument
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public List<CompetitionIndexEntryDocument> Competitions { get; set; } = new();
}

public class CompetitionIndexEntryDocument
{
	public int Year { get; set; }
	public string Slug { get; set; } = string.Empty;
	public int SeasonId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public string SeasonPath { get; set; } = string.Empty;
}
