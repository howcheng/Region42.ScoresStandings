namespace Region42.ScoresStandings.VolunteerSync.Services;

/// <summary>
/// Builds request URIs for cgisports. Root-relative paths must not use <see cref="Uri.TryCreate(string, UriKind, out Uri?)"/>
/// with <see cref="UriKind.Absolute"/> on Linux, where they become file:// URLs.
/// </summary>
public static class CgiSportsUriHelper
{
	public static Uri Combine(string sourceBaseUrl, string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceBaseUrl);
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			|| path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			return new Uri(path);
		}

		return new Uri(new Uri(sourceBaseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));
	}
}
