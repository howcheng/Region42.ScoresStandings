namespace Region42.ScoresStandings.VolunteerSync.Configuration;

public class VolunteerSyncOptions
{
	public const string SectionName = "VolunteerSync";

	public string SourceBaseUrl { get; set; } = string.Empty;
	public string SourceLoginPath { get; set; } = "/login";
	public string SourceDownloadPath { get; set; } = string.Empty;
	public string SourceUsername { get; set; } = string.Empty;
	public string SourcePassword { get; set; } = string.Empty;
	public string TargetApiBaseUrl { get; set; } = string.Empty;
	public string TargetImportPath { get; set; } = "/api/volunteer-points/import";
	public int DivisionId { get; set; }
	public bool DryRun { get; set; } = true;
}
