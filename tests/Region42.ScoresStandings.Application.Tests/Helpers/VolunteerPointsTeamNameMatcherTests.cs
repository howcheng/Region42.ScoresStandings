using FluentAssertions;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Tests.Helpers;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

public class VolunteerPointsTeamNameMatcherTests
{
	[Fact]
	public void TryMatch_ResolvesCoachParentheticalFormat()
	{
		var teams = new[]
		{
			TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "10UB04 Sharks (Timen)")
		};

		var match = VolunteerPointsTeamNameMatcher.TryMatch("10UB04 (Timen)", teams);
		match.Should().NotBeNull();
		match!.Id.Should().Be(1);
	}

	[Fact]
	public void TryMatch_ResolvesBareDivisionTeamNumber()
	{
		var teams = new[]
		{
			TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "10UB01 Sharks (Giron)")
		};

		var match = VolunteerPointsTeamNameMatcher.TryMatch("10UB01", teams);
		match.Should().NotBeNull();
		match!.Id.Should().Be(2);
	}

	[Fact]
	public void TryMatch_ResolvesTeamNumberOnly()
	{
		var teams = new[]
		{
			TestDataBuilder.CreateTeam(id: 3, divisionId: 1, name: "10UB04 Sharks (Timen)"),
			TestDataBuilder.CreateTeam(id: 4, divisionId: 1, name: "10UB05 (Cull)")
		};

		VolunteerPointsTeamNameMatcher.TryMatch("04", teams)!.Id.Should().Be(3);
		VolunteerPointsTeamNameMatcher.TryMatch("4", teams)!.Id.Should().Be(3);
		VolunteerPointsTeamNameMatcher.TryMatch("05 (Cull)", teams)!.Id.Should().Be(4);
	}

	[Fact]
	public void TryMatch_DoesNotMatchCoachNameOnly()
	{
		var teams = new[]
		{
			TestDataBuilder.CreateTeam(id: 5, divisionId: 1, name: "10UB03 Sharks (Cassaro)")
		};

		VolunteerPointsTeamNameMatcher.TryMatch("Cassaro", teams).Should().BeNull();
		VolunteerPointsTeamNameMatcher.IsLikelyCoachNameOnly("Cassaro").Should().BeTrue();
	}
}