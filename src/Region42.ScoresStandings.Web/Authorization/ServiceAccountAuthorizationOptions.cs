namespace Region42.ScoresStandings.Web.Authorization;

public class ServiceAccountAuthorizationOptions
{
	public const string SectionName = "Authentication:ServiceAccount";

	public string AllowedServiceAccountEmail { get; set; } = string.Empty;
	public string JwtAudience { get; set; } = string.Empty;
}
