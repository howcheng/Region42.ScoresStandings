using System.Text.RegularExpressions;

using Region42.ScoresStandings.Domain.Entities;



namespace Region42.ScoresStandings.Application.Helpers;



/// <summary>

/// Resolves external team labels from the cgisports export to internal teams.

/// </summary>

public static class VolunteerPointsTeamNameMatcher

{

	public static Team? TryMatch(string externalTeamName, IReadOnlyList<Team> teamsInDivision)

	{

		if (string.IsNullOrWhiteSpace(externalTeamName) || teamsInDivision.Count == 0)

		{

			return null;

		}



		var trimmed = externalTeamName.Trim();

		if (IsLikelyCoachNameOnly(trimmed))

		{

			return null;

		}



		var normalizedExternal = Normalize(trimmed);



		foreach (var team in teamsInDivision)

		{

			if (string.Equals(team.Name, trimmed, StringComparison.OrdinalIgnoreCase))

			{

				return team;

			}



			if (string.Equals(Normalize(team.Name), normalizedExternal, StringComparison.OrdinalIgnoreCase))

			{

				return team;

			}

		}



		var prefixMatch = TryMatchDivisionPrefix(trimmed, teamsInDivision);

		if (prefixMatch != null)

		{

			return prefixMatch;

		}



		return TryMatchTeamNumberOnly(trimmed, teamsInDivision);

	}



	/// <summary>

	/// Labels that are only letters (coach last names with no team number) are not auto-matched.

	/// </summary>

	public static bool IsLikelyCoachNameOnly(string externalName)

	{

		if (string.IsNullOrWhiteSpace(externalName))

		{

			return false;

		}



		var trimmed = externalName.Trim();

		if (trimmed.Any(char.IsDigit))

		{

			return false;

		}



		if (LeadingDivisionCodeRegex().IsMatch(trimmed))

		{

			return false;

		}



		if (RegionalTeamRegex().IsMatch(trimmed))

		{

			return false;

		}



		return trimmed.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || c == '-' || c == '\'');

	}



	private static Team? TryMatchDivisionPrefix(string externalName, IReadOnlyList<Team> teams)

	{

		var codeMatch = Regex.Match(

			externalName,

			@"^(10U[BG]|12U[BG]|14U[BG])\s*0?(\d{1,2})\b",

			RegexOptions.IgnoreCase);

		if (!codeMatch.Success)

		{

			codeMatch = Regex.Match(

				externalName,

				@"^(10U[BG]|12U[BG]|14U[BG])0?(\d{1,2})\b",

				RegexOptions.IgnoreCase);

		}



		if (!codeMatch.Success)

		{

			return null;

		}



		var divisionCode = codeMatch.Groups[1].Value.ToUpperInvariant();

		var teamNumber = codeMatch.Groups[2].Value.PadLeft(2, '0');



		return SelectUniqueByDivisionAndNumber(teams, divisionCode, teamNumber);

	}



	private static Team? TryMatchTeamNumberOnly(string externalName, IReadOnlyList<Team> teams)

	{

		var numberMatch = Regex.Match(externalName, @"^0?(\d{1,2})(?:\s|\(|$)", RegexOptions.IgnoreCase);

		if (!numberMatch.Success)

		{

			return null;

		}



		var teamNumber = numberMatch.Groups[1].Value.PadLeft(2, '0');

		var candidates = teams

			.Where(t => string.Equals(TryGetTeamNumberFromRosterName(t.Name), teamNumber, StringComparison.Ordinal))

			.ToList();



		return candidates.Count == 1 ? candidates[0] : null;

	}



	private static Team? SelectUniqueByDivisionAndNumber(

		IReadOnlyList<Team> teams,

		string divisionCode,

		string teamNumber)

	{

		var candidates = teams.Where(t =>

		{

			if (!t.Name.Contains(divisionCode, StringComparison.OrdinalIgnoreCase))

			{

				return false;

			}



			return string.Equals(TryGetTeamNumberFromRosterName(t.Name), teamNumber, StringComparison.Ordinal);

		}).ToList();



		return candidates.Count == 1 ? candidates[0] : null;

	}



	private static string? TryGetTeamNumberFromRosterName(string teamName)

	{

		var match = RosterTeamNumberRegex().Match(teamName);

		if (!match.Success)

		{

			return null;

		}



		return match.Groups[2].Value.PadLeft(2, '0');

	}



	private static string Normalize(string value)

	{

		var chars = value.Where(c => !char.IsPunctuation(c) || c == '(' || c == ')').ToArray();

		return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));

	}



	private static Regex LeadingDivisionCodeRegex() => DivisionPrefixRegex.Value;



	private static Regex RegionalTeamRegex() => RegionalRegex.Value;



	private static Regex RosterTeamNumberRegex() => RosterNumberRegex.Value;



	private static readonly Lazy<Regex> DivisionPrefixRegex = new(() =>

		new Regex(@"^(\d{2}U[BG])", RegexOptions.IgnoreCase | RegexOptions.Compiled));



	private static readonly Lazy<Regex> RegionalRegex = new(() =>

		new Regex(@"^R\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled));



	private static readonly Lazy<Regex> RosterNumberRegex = new(() =>

		new Regex(@"^(10U[BG]|12U[BG]|14U[BG])\s*0?(\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled));

}
