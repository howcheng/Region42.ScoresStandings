using FluentAssertions;
using Region42.ScoresStandings.Application.Helpers;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

public class GameRoundCalculatorTests
{
	[Fact]
	public void BuildRoundByDateMap_WithMidWeekAndSundayRescheduledGames_AssignsSameRoundAsWeek()
	{
		var scheduledDates = new[]
		{
			new DateTime(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 9, 12, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 9, 19, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 20, 1, 30, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 25, 17, 45, 0, DateTimeKind.Utc),
			new DateTime(2026, 10, 31, 15, 0, 0, DateTimeKind.Utc)
		};

		var map = GameRoundCalculator.BuildRoundByDateMap(scheduledDates);

		map["2026-08-29"].Should().Be(1);
		map["2026-09-12"].Should().Be(2);
		map["2026-09-19"].Should().Be(3);
		map["2026-09-26"].Should().Be(4);
		map["2026-10-03"].Should().Be(5);
		map["2026-10-10"].Should().Be(6);
		map["2026-10-17"].Should().Be(7);
		map["2026-10-20"].Should().Be(8);
		map["2026-10-24"].Should().Be(8);
		map["2026-10-25"].Should().Be(9);
		map["2026-10-31"].Should().Be(9);
	}

	[Fact]
	public void GetRoundForDate_WhenDateInSameWeekAsMappedRound_ReturnsWeekRound()
	{
		var roundByDate = new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["2026-10-17"] = 7,
			["2026-10-24"] = 8,
			["2026-10-31"] = 9
		};

		GameRoundCalculator.GetRoundForDate(new DateTime(2026, 10, 20), roundByDate).Should().Be(8);
		GameRoundCalculator.GetRoundForDate(new DateTime(2026, 10, 24), roundByDate).Should().Be(8);
		GameRoundCalculator.GetRoundForDate(new DateTime(2026, 10, 25), roundByDate).Should().Be(9);
		GameRoundCalculator.GetRoundForDate(new DateTime(2026, 10, 31), roundByDate).Should().Be(9);
		GameRoundCalculator.GetRoundForDate(new DateTime(2026, 9, 5), roundByDate).Should().BeNull();
	}
}
