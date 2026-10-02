namespace Region42.ScoresStandings.Application.Helpers;

/// <summary>
/// Maps game dates to round numbers using the same rules as CSV import.
/// </summary>
public static class GameRoundCalculator
{
	public static IReadOnlyDictionary<string, int> BuildRoundByDateMap(IEnumerable<DateTime> scheduledDateTimes)
	{
		var gameDates = scheduledDateTimes
			.Select(d => d.Date)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		var map = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; i < gameDates.Count; i++)
		{
			map[gameDates[i].ToString("yyyy-MM-dd")] = i + 1;
		}

		return map;
	}

	public static int? GetRoundForDate(DateTime date, IReadOnlyDictionary<string, int> roundByDate)
	{
		return roundByDate.TryGetValue(date.Date.ToString("yyyy-MM-dd"), out var round) ? round : null;
	}
}
