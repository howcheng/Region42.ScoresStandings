using FluentAssertions;
using Region42.ScoresStandings.Application.Helpers;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

public class CgiSportsTeamLabelHelperTests
{
	[Theory]
	[InlineData("10UB04 (Timen)", "10UB", true)]
	[InlineData("12UB02 (Landes)", "10UB", false)]
	[InlineData("R121A Bulls (Subramanian)", "14UB", true)]
	[InlineData("Cassaro", "12UB", true)]
	[InlineData("10UB01", "10UB", true)]
	public void TeamLabelAppliesToDivision_FiltersCrossDivisionLabels(string team, string division, bool expected)
	{
		CgiSportsTeamLabelHelper.TeamLabelAppliesToDivision(team, division).Should().Be(expected);
	}
}
