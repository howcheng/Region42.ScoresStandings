using System.Globalization;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Services;

public class VolunteerPointsExcelParser : IVolunteerPointsFileParser
{
	private const string TeamPointsWorksheetName = "team_points_wk";

	private readonly ILogger<VolunteerPointsExcelParser> _logger;

	public VolunteerPointsExcelParser(ILogger<VolunteerPointsExcelParser> logger)
	{
		_logger = logger;
	}

	public IReadOnlyList<VolunteerPointsRawRow> Parse(Stream fileStream)
	{
		ArgumentNullException.ThrowIfNull(fileStream);

		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		using var reader = ExcelReaderFactory.CreateReader(fileStream);
		var dataSet = reader.AsDataSet();
		var table = dataSet.Tables.Cast<System.Data.DataTable>()
			.FirstOrDefault(t => t.TableName.Equals(TeamPointsWorksheetName, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException(
				$"Volunteer points export is missing the {TeamPointsWorksheetName} worksheet.");

		if (table.Rows.Count == 0)
		{
			return Array.Empty<VolunteerPointsRawRow>();
		}

		var headerRow = table.Rows[0];
		var columnIndex = BuildColumnIndex(headerRow);
		ValidateRequiredColumns(columnIndex);

		var rows = new List<VolunteerPointsRawRow>();

		for (var rowIndex = 1; rowIndex < table.Rows.Count; rowIndex++)
		{
			var row = table.Rows[rowIndex];
			var team = GetString(row, columnIndex["Team"]);
			if (string.IsNullOrWhiteSpace(team))
			{
				continue;
			}

			if (!TryGetLeadingDivisionCode(team, out var divisionCode))
			{
				_logger.LogWarning(
					"Skipping team_points_wk row {RowIndex}: cannot determine division for team {Team}",
					rowIndex + 1,
					team);
				continue;
			}

			if (!TryParseDate(row[columnIndex["Week Of"]], out var date))
			{
				_logger.LogWarning(
					"Skipping team_points_wk row {RowIndex}: invalid week-of date for team {Team}",
					rowIndex + 1,
					team);
				continue;
			}

			var points = ParsePoints(row[columnIndex["Allowed"]]);
			if (points < 0)
			{
				_logger.LogWarning(
					"Skipping team_points_wk row {RowIndex}: negative allowed points for team {Team}",
					rowIndex + 1,
					team);
				continue;
			}

			rows.Add(new VolunteerPointsRawRow
			{
				DivisionCode = divisionCode,
				Team = team.Trim(),
				Date = date.Date,
				Points = points
			});
		}

		return rows;
	}

	private static bool TryGetLeadingDivisionCode(string teamLabel, out string cgiDivisionCode)
	{
		if (VolunteerPointsTeamNameMatcher.IsLikelyCoachNameOnly(teamLabel))
		{
			cgiDivisionCode = string.Empty;
			return false;
		}

		return CgiSportsTeamLabelHelper.TryGetLeadingDivisionCode(teamLabel, out cgiDivisionCode);
	}

	private static Dictionary<string, int> BuildColumnIndex(System.Data.DataRow headerRow)
	{
		var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < headerRow.ItemArray.Length; i++)
		{
			var name = headerRow[i]?.ToString()?.Trim();
			if (!string.IsNullOrEmpty(name))
			{
				map[name] = i;
			}
		}

		return map;
	}

	private static void ValidateRequiredColumns(IReadOnlyDictionary<string, int> columnIndex)
	{
		foreach (var required in new[] { "Team", "Week Of", "Allowed" })
		{
			if (!columnIndex.ContainsKey(required))
			{
				throw new InvalidOperationException($"Volunteer points export is missing required column '{required}'.");
			}
		}
	}

	private static string GetString(System.Data.DataRow row, int column)
	{
		return row[column]?.ToString()?.Trim() ?? string.Empty;
	}

	private static decimal ParsePoints(object? cellValue)
	{
		if (cellValue == null || cellValue == DBNull.Value)
		{
			return 0m;
		}

		if (cellValue is double d)
		{
			return Convert.ToDecimal(d);
		}

		if (cellValue is float f)
		{
			return Convert.ToDecimal(f);
		}

		if (cellValue is decimal dec)
		{
			return dec;
		}

		var text = cellValue.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return 0m;
		}

		return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: 0m;
	}

	private static bool TryParseDate(object? cellValue, out DateTime date)
	{
		date = default;
		if (cellValue == null || cellValue == DBNull.Value)
		{
			return false;
		}

		if (cellValue is DateTime dt)
		{
			date = dt.Date;
			return true;
		}

		if (cellValue is double oaDate)
		{
			date = DateTime.FromOADate(oaDate).Date;
			return true;
		}

		var text = cellValue.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}

		return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
			|| DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
	}
}
