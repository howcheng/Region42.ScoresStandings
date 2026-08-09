using Region42.ScoresStandings.Application.Helpers;
using Xunit;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

public class TimezoneHelperTests
{
	[Fact]
	public void ToUtc_ConvertsPacificStandardTimeCorrectly()
	{
		// Arrange - January is PST (UTC-8)
		var pacificTime = new DateTime(2025, 1, 15, 10, 0, 0); // 10 AM PST

		// Act
		var utcTime = TimezoneHelper.ToUtc(pacificTime);

		// Assert - should be 6 PM UTC (10 AM + 8 hours)
		Assert.Equal(new DateTime(2025, 1, 15, 18, 0, 0, DateTimeKind.Utc), utcTime);
		Assert.Equal(DateTimeKind.Utc, utcTime.Kind);
	}

	[Fact]
	public void ToUtc_ConvertsPacificDaylightTimeCorrectly()
	{
		// Arrange - June is PDT (UTC-7)
		var pacificTime = new DateTime(2025, 6, 15, 10, 0, 0); // 10 AM PDT

		// Act
		var utcTime = TimezoneHelper.ToUtc(pacificTime);

		// Assert - should be 5 PM UTC (10 AM + 7 hours)
		Assert.Equal(new DateTime(2025, 6, 15, 17, 0, 0, DateTimeKind.Utc), utcTime);
		Assert.Equal(DateTimeKind.Utc, utcTime.Kind);
	}

	[Fact]
	public void ToPacificTime_ConvertsUtcToStandardTimeCorrectly()
	{
		// Arrange - January is PST
		var utcTime = new DateTime(2025, 1, 15, 18, 0, 0, DateTimeKind.Utc); // 6 PM UTC

		// Act
		var pacificTime = TimezoneHelper.ToPacificTime(utcTime);

		// Assert - should be 10 AM PST
		Assert.Equal(10, pacificTime.Hour);
		Assert.Equal(new DateTime(2025, 1, 15, 10, 0, 0), pacificTime);
	}

	[Fact]
	public void ToPacificTime_ConvertsUtcToDaylightTimeCorrectly()
	{
		// Arrange - June is PDT
		var utcTime = new DateTime(2025, 6, 15, 17, 0, 0, DateTimeKind.Utc); // 5 PM UTC

		// Act
		var pacificTime = TimezoneHelper.ToPacificTime(utcTime);

		// Assert - should be 10 AM PDT
		Assert.Equal(10, pacificTime.Hour);
		Assert.Equal(new DateTime(2025, 6, 15, 10, 0, 0), pacificTime);
	}

	[Fact]
	public void GetTimezoneAbbreviation_ReturnsPSTForWinter()
	{
		// Arrange
		var utcTime = new DateTime(2025, 1, 15, 18, 0, 0, DateTimeKind.Utc);

		// Act
		var abbreviation = TimezoneHelper.GetTimezoneAbbreviation(utcTime);

		// Assert
		Assert.Equal("PST", abbreviation);
	}

	[Fact]
	public void GetTimezoneAbbreviation_ReturnsPDTForSummer()
	{
		// Arrange
		var utcTime = new DateTime(2025, 6, 15, 17, 0, 0, DateTimeKind.Utc);

		// Act
		var abbreviation = TimezoneHelper.GetTimezoneAbbreviation(utcTime);

		// Assert
		Assert.Equal("PDT", abbreviation);
	}

	[Fact]
	public void FormatPacificTime_FormatsCorrectly()
	{
		// Arrange
		var utcTime = new DateTime(2025, 3, 15, 17, 30, 0, DateTimeKind.Utc); // 5:30 PM UTC in March (PDT)

		// Act
		var formatted = TimezoneHelper.FormatPacificTime(utcTime);

		// Assert - should be 10:30 AM PDT
		Assert.Equal("3/15/2025 10:30 AM", formatted);
	}

	[Fact]
	public void RoundTrip_PreservesDateTime()
	{
		// Arrange
		var originalPacific = new DateTime(2025, 3, 15, 10, 30, 0);

		// Act - Convert to UTC and back
		var utc = TimezoneHelper.ToUtc(originalPacific);
		var backToPacific = TimezoneHelper.ToPacificTime(utc);

		// Assert
		Assert.Equal(originalPacific, backToPacific);
	}
}
