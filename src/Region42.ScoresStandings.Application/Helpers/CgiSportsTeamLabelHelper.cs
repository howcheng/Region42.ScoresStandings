using System.Text.RegularExpressions;

namespace Region42.ScoresStandings.Application.Helpers;

/// <summary>
/// Parses team labels from the cgisports volunteer export.
/// </summary>
public static partial class CgiSportsTeamLabelHelper
{
	/// <summary>
	/// Returns true when the row should be included in import for <paramref name="cgiDivisionCode"/>.
	/// Rows with a leading division code (10UB, 12UG, 08UB, …) must match the target division.
	/// Regional (R…) and coach-only labels are left for team-name matching.
	/// </summary>
	public static bool TeamLabelAppliesToDivision(string teamLabel, string cgiDivisionCode)
	{
		if (string.IsNullOrWhiteSpace(teamLabel) || string.IsNullOrWhiteSpace(cgiDivisionCode))
		{
			return false;
		}

		var trimmed = teamLabel.Trim();
		if (RegionalTeamRegex().IsMatch(trimmed))
		{
			return true;
		}

		var match = LeadingDivisionCodeRegex().Match(trimmed);
		if (!match.Success)
		{
			return true;
		}

		return match.Groups[1].Value.Equals(cgiDivisionCode, StringComparison.OrdinalIgnoreCase);
	}

	[GeneratedRegex(@"^R\d+", RegexOptions.IgnoreCase)]
	private static partial Regex RegionalTeamRegex();

	[GeneratedRegex(@"^(\d{2}U[BG])", RegexOptions.IgnoreCase)]
	private static partial Regex LeadingDivisionCodeRegex();
}
