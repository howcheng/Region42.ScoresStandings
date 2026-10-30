using System.Globalization;

namespace Region42.ScoresStandings.Application.Helpers;

/// <summary>
/// Maps game dates to round numbers by calendar week (Sunday through Saturday)
/// so that mid-week or Sunday makeup games map to the round of the week in which they are played.
/// </summary>
public static class GameRoundCalculator
{
	public static IReadOnlyDictionary<string, int> BuildRoundByDateMap(IEnumerable<DateTime> scheduledDateTimes)
	{
		ArgumentNullException.ThrowIfNull(scheduledDateTimes);

		var gameDates = scheduledDateTimes
			.Select(d => d.Date)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		var weekStarts = gameDates
			.Select(StartOfWeek)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		var roundByWeekStart = new Dictionary<DateTime, int>(weekStarts.Count);
		for (var i = 0; i < weekStarts.Count; i++)
		{
			roundByWeekStart[weekStarts[i]] = i + 1;
		}

		var map = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var gameDate in gameDates)
		{
			map[gameDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = roundByWeekStart[StartOfWeek(gameDate)];
		}

		return map;
	}

	public static int? GetRoundForDate(DateTime date, IReadOnlyDictionary<string, int> roundByDate)
	{
		ArgumentNullException.ThrowIfNull(roundByDate);

		var dateKey = date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (roundByDate.TryGetValue(dateKey, out var round))
		{
			return round;
		}

		var targetWeekStart = StartOfWeek(date.Date);
		foreach (var (key, mappedRound) in roundByDate)
		{
			if (DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mappedDate) &&
				StartOfWeek(mappedDate) == targetWeekStart)
			{
				return mappedRound;
			}
		}

		return null;
	}

	private static DateTime StartOfWeek(DateTime date)
	{
		var diff = (int)date.DayOfWeek;
		return date.AddDays(-diff).Date;
	}
}
