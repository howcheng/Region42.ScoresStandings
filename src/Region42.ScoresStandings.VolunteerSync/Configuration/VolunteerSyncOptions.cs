namespace Region42.ScoresStandings.VolunteerSync.Configuration;

public class VolunteerSyncOptions
{
	public const string SectionName = "VolunteerSync";

	public string SourceBaseUrl { get; set; } = string.Empty;
	public string SourceLoginPath { get; set; } = "/ref/2130/";
	public string SourceAssignmentLogPath { get; set; } = "/ref/2130?user={0}&assignment_log=1";
	public string SourceDownloadPath { get; set; } = string.Empty;
	public string SourceUsername { get; set; } = string.Empty;
	public string SourcePassword { get; set; } = string.Empty;
	public string TargetApiBaseUrl { get; set; } = string.Empty;
	/// <summary>
	/// Optional bearer token for local development (metadata server is unavailable off GCP).
	/// Set via user secrets or gcloud auth print-identity-token with audience = TargetApiBaseUrl.
	/// </summary>
	public string TargetIdentityToken { get; set; } = string.Empty;
	public string TargetImportPath { get; set; } = "/api/volunteer-points/import";
	public int DivisionId { get; set; }
	public bool DryRun { get; set; } = true;
}
