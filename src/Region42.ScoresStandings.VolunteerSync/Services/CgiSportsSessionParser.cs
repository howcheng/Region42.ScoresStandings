using System.Text.RegularExpressions;

namespace Region42.ScoresStandings.VolunteerSync.Services;

public static class CgiSportsSessionParser
{
	private static readonly Regex UserInputRegex = new(
		"""name=["']user["'][^>]*value=["']([^"']+)["']""",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex UidScriptRegex = new(
		"""uid=["']([^"']+)["']""",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static string? TryExtractSessionUser(Uri? responseUri, string? responseBody)
	{
		var fromUri = TryExtractUserFromUri(responseUri);
		if (!string.IsNullOrWhiteSpace(fromUri))
		{
			return fromUri;
		}

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			return null;
		}

		var inputMatch = UserInputRegex.Match(responseBody);
		if (inputMatch.Success && !string.IsNullOrWhiteSpace(inputMatch.Groups[1].Value))
		{
			return inputMatch.Groups[1].Value.Trim();
		}

		var uidMatch = UidScriptRegex.Match(responseBody);
		if (uidMatch.Success && !string.IsNullOrWhiteSpace(uidMatch.Groups[1].Value))
		{
			return uidMatch.Groups[1].Value.Trim();
		}

		return null;
	}

	public static string? TryExtractUserFromUri(Uri? uri)
	{
		if (uri == null)
		{
			return null;
		}

		var query = uri.Query.TrimStart('?');
		if (string.IsNullOrEmpty(query))
		{
			return null;
		}

		foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var kv = part.Split('=', 2);
			if (kv.Length == 2
				&& kv[0].Equals("user", StringComparison.OrdinalIgnoreCase)
				&& !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(kv[1])))
			{
				return Uri.UnescapeDataString(kv[1]);
			}
		}

		return null;
	}
}
