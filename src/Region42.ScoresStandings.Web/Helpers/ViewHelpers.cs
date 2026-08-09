using Region42.ScoresStandings.Application.Helpers;

namespace Region42.ScoresStandings.Web.Helpers;

/// <summary>
/// View helper extensions for common display operations.
/// </summary>
public static class ViewHelpers
{
	/// <summary>
	/// Format a UTC DateTime for display in Pacific Time.
	/// </summary>
	public static string ToPacificTime(this DateTime utcDateTime, string format = "M/d/yyyy h:mm tt")
	{
		return TimezoneHelper.FormatPacificTime(utcDateTime, format);
	}

	/// <summary>
	/// Format a UTC DateTime with timezone abbreviation.
	/// Example: "3/15/2025 10:00 AM PDT"
	/// </summary>
	public static string ToPacificTimeWithZone(this DateTime utcDateTime)
	{
		var formatted = TimezoneHelper.FormatPacificTime(utcDateTime);
		var zone = TimezoneHelper.GetTimezoneAbbreviation(utcDateTime);
		return $"{formatted} {zone}";
	}

	/// <summary>
	/// Format date only in Pacific Time.
	/// </summary>
	public static string ToPacificDate(this DateTime utcDateTime, string format = "M/d/yyyy")
	{
		var pacificTime = TimezoneHelper.ToPacificTime(utcDateTime);
		return pacificTime.ToString(format);
	}

	/// <summary>
	/// Format time only in Pacific Time.
	/// </summary>
	public static string ToPacificTimeOnly(this DateTime utcDateTime, string format = "h:mm tt")
	{
		var pacificTime = TimezoneHelper.ToPacificTime(utcDateTime);
		return pacificTime.ToString(format);
	}
}
