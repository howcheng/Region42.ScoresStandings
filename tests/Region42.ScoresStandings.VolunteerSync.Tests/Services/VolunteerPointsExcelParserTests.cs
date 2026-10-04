using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class VolunteerPointsExcelParserTests
{
	[Fact]
	public void Parse_ReadsTeamPointsWeeklySheet_UsingAllowedPoints()
	{
		var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "assignment_log.xls");
		File.Exists(fixturePath).Should().BeTrue("test fixture assignment_log.xls should be copied to output");

		using var stream = File.OpenRead(fixturePath);
		var parser = new VolunteerPointsExcelParser(Mock.Of<ILogger<VolunteerPointsExcelParser>>());
		var rows = parser.Parse(stream);

		rows.Should().NotBeEmpty();
		rows.Should().Contain(r => r.Points == 0.5m);
		rows.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.DivisionCode));
		rows.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Team));

		var landesAug29 = rows.Should().ContainSingle(r =>
			r.Team == "12UB02 (Landes)" &&
			r.DivisionCode == "12UB" &&
			r.Date == new DateTime(2026, 8, 29)).Subject;
		landesAug29.Points.Should().Be(2m, "Allowed caps weekly volunteer credit independent of assignment count");
	}
}
