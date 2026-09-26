using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class VolunteerPointsExcelParserTests
{
	[Fact]
	public void Parse_ReturnsEmptyEntries_UntilSourceFormatIsMapped()
	{
		using var workbook = new XLWorkbook();
		workbook.AddWorksheet("Volunteer Points");

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		stream.Position = 0;

		var parser = new VolunteerPointsExcelParser(Mock.Of<ILogger<VolunteerPointsExcelParser>>());
		var result = parser.Parse(stream, divisionId: 3);

		result.DivisionId.Should().Be(3);
		result.Entries.Should().BeEmpty();
	}
}
