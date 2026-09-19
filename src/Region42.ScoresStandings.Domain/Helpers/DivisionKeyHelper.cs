using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Helpers;

public static class DivisionKeyHelper
{
	public static string ToKey(AgeGroup ageGroup, Gender gender)
	{
		var age = ageGroup switch
		{
			AgeGroup.U10 => "10u",
			AgeGroup.U12 => "12u",
			AgeGroup.U14 => "14u",
			_ => ageGroup.ToString().ToLowerInvariant()
		};

		var genderKey = gender == Gender.Boys ? "boys" : "girls";
		return $"{age}-{genderKey}";
	}

	public static AgeGroup ParseAgeGroup(string value)
	{
		return value.ToUpperInvariant() switch
		{
			"U10" or "10U" => AgeGroup.U10,
			"U12" or "12U" => AgeGroup.U12,
			"U14" or "14U" => AgeGroup.U14,
			_ => Enum.Parse<AgeGroup>(value, ignoreCase: true)
		};
	}

	public static Gender ParseGender(string value)
	{
		return value.Equals("Boys", StringComparison.OrdinalIgnoreCase) ? Gender.Boys : Gender.Girls;
	}

	public static string ToAgeGroupString(AgeGroup ageGroup)
	{
		return ageGroup switch
		{
			AgeGroup.U10 => "U10",
			AgeGroup.U12 => "U12",
			AgeGroup.U14 => "U14",
			_ => ageGroup.ToString()
		};
	}

	public static string ToGenderString(Gender gender)
	{
		return gender == Gender.Boys ? "Boys" : "Girls";
	}

	public static string GetCompetitionPath(int year, string competitionSlug)
	{
		return $"{year}/{competitionSlug}";
	}

	public static string GetSeasonObjectPath(int year, string competitionSlug)
	{
		return $"{GetCompetitionPath(year, competitionSlug)}/season.json";
	}

	public static string GetDivisionObjectPath(int year, string competitionSlug, string divisionKey)
	{
		return $"{GetCompetitionPath(year, competitionSlug)}/{divisionKey}.json";
	}
}
