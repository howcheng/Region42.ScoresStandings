using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Services;

/// <summary>
/// Parses volunteer points exports from Excel.
/// Column layout mapping will be implemented once the source file format is documented.
/// </summary>
public class VolunteerPointsExcelParser : IVolunteerPointsFileParser
{
	private readonly ILogger<VolunteerPointsExcelParser> _logger;

	public VolunteerPointsExcelParser(ILogger<VolunteerPointsExcelParser> logger)
	{
		_logger = logger;
	}

	public VolunteerPointsBulkUpdateDto Parse(Stream fileStream, int divisionId)
	{
		ArgumentNullException.ThrowIfNull(fileStream);

		using var workbook = new XLWorkbook(fileStream);
		var worksheet = workbook.Worksheets.FirstOrDefault()
			?? throw new InvalidOperationException("Volunteer points export does not contain any worksheets.");

		_logger.LogWarning(
			"Volunteer points Excel parser is not yet mapped to the source format. Worksheet '{WorksheetName}' will produce zero entries until mapping is implemented.",
			worksheet.Name);

		// TODO: Map source columns (team name, round columns or rows) once export format is known.
		_ = worksheet;

		return new VolunteerPointsBulkUpdateDto
		{
			DivisionId = divisionId,
			Entries = new List<VolunteerPointsEntryDto>()
		};
	}
}
