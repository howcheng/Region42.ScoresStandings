namespace Region42.ScoresStandings.Application.Helpers;

/// <summary>
/// Helper for timezone conversions between UTC (storage) and Pacific Time (display).
/// AYSO Region 42 operates in Pacific Time Zone (America/Los_Angeles).
/// </summary>
public static class TimezoneHelper
{
	private static readonly TimeZoneInfo PacificZone = 
		TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

	/// <summary>
	/// Convert UTC time to Pacific Time for display.
	/// </summary>
	/// <param name="utcDateTime">DateTime in UTC (from database)</param>
	/// <returns>DateTime in Pacific Time</returns>
	public static DateTime ToPacificTime(DateTime utcDateTime)
	{
		if (utcDateTime.Kind != DateTimeKind.Utc)
		{
			throw new ArgumentException("Expected UTC DateTime", nameof(utcDateTime));
		}

		return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, PacificZone);
	}

	/// <summary>
	/// Convert Pacific Time to UTC for storage.
	/// </summary>
	/// <param name="pacificDateTime">DateTime in Pacific Time</param>
	/// <returns>DateTime in UTC</returns>
	public static DateTime ToUtc(DateTime pacificDateTime)
	{
		// Treat as unspecified so we can convert from Pacific
		var unspecified = DateTime.SpecifyKind(pacificDateTime, DateTimeKind.Unspecified);
		return TimeZoneInfo.ConvertTimeToUtc(unspecified, PacificZone);
	}

	/// <summary>
	/// Format a UTC DateTime as Pacific Time for display.
	/// </summary>
	/// <param name="utcDateTime">DateTime in UTC</param>
	/// <param name="format">Optional format string (default: "M/d/yyyy h:mm tt")</param>
	/// <returns>Formatted string in Pacific Time</returns>
	public static string FormatPacificTime(DateTime utcDateTime, string format = "M/d/yyyy h:mm tt")
	{
		var pacificTime = ToPacificTime(utcDateTime);
		return pacificTime.ToString(format);
	}

	/// <summary>
	/// Get the timezone abbreviation for a given UTC time (PST or PDT).
	/// </summary>
	public static string GetTimezoneAbbreviation(DateTime utcDateTime)
	{
		var pacificTime = ToPacificTime(utcDateTime);
		return PacificZone.IsDaylightSavingTime(pacificTime) ? "PDT" : "PST";
	}
}
