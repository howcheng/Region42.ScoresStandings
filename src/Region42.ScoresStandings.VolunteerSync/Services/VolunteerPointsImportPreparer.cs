using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Services;

public class VolunteerPointsImportPreparer
{
	private readonly ILogger<VolunteerPointsImportPreparer> _logger;

	public VolunteerPointsImportPreparer(ILogger<VolunteerPointsImportPreparer> logger)
	{
		_logger = logger;
	}

	public IReadOnlyList<VolunteerPointsBulkUpdateDto> BuildDivisionImports(
		IReadOnlyList<VolunteerPointsRawRow> rawRows,
		VolunteerPointsSyncContextDto syncContext)
	{
		var knownCodes = syncContext.Divisions
			.ToDictionary(d => d.CgiDivisionCode, d => d, StringComparer.OrdinalIgnoreCase);

		var unknownDivisions = rawRows
			.Select(r => r.DivisionCode)
			.Where(code => !knownCodes.ContainsKey(code))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		foreach (var code in unknownDivisions)
		{
			_logger.LogInformation("Skipping volunteer points rows for unknown division code {DivisionCode}", code);
		}

		var imports = new List<VolunteerPointsBulkUpdateDto>();

		foreach (var division in syncContext.Divisions)
		{
			var divisionRows = rawRows
				.Where(r => r.DivisionCode.Equals(division.CgiDivisionCode, StringComparison.OrdinalIgnoreCase))
				.ToList();

			if (divisionRows.Count == 0)
			{
				continue;
			}

			var aggregatedByTeamRound = new Dictionary<(string Team, int Round), decimal>();

			foreach (var row in divisionRows)
			{
				if (!CgiSportsTeamLabelHelper.TeamLabelAppliesToDivision(row.Team, division.CgiDivisionCode))
				{
					continue;
				}

				var dateKey = row.Date.ToString("yyyy-MM-dd");
				if (!division.RoundByDate.TryGetValue(dateKey, out var round))
				{
					_logger.LogWarning(
						"No round mapping for date {Date} in division {DivisionCode}; skipping team {Team}",
						dateKey,
						division.CgiDivisionCode,
						row.Team);
					continue;
				}

				var key = (row.Team, round);
				aggregatedByTeamRound[key] = aggregatedByTeamRound.GetValueOrDefault(key) + row.Points;
			}

			if (aggregatedByTeamRound.Count == 0)
			{
				continue;
			}

			imports.Add(new VolunteerPointsBulkUpdateDto
			{
				DivisionId = division.DivisionId,
				Entries = aggregatedByTeamRound.Select(kvp => new VolunteerPointsEntryDto
				{
					TeamName = kvp.Key.Team,
					Round = kvp.Key.Round,
					Points = kvp.Value
				}).ToList()
			});
		}

		return imports;
	}
}
