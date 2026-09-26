namespace Region42.ScoresStandings.Application.DTOs;

public class VolunteerPointsImportResultDto
{
	public bool DryRun { get; set; }
	public int ImportedCount { get; set; }
	public int SkippedCount { get; set; }
	public List<string> UnmatchedTeamNames { get; set; } = new();
	public List<string> ValidationErrors { get; set; } = new();
	public List<int> AffectedDivisionIds { get; set; } = new();
}
