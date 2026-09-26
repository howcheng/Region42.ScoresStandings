using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.VolunteerSync.Models;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class VolunteerPointsTeamMatcherTests
{
	[Fact]
	public async Task MatchEntriesAsync_PreservesExistingTeamIds()
	{
		var matcher = new VolunteerPointsTeamMatcher(Mock.Of<ILogger<VolunteerPointsTeamMatcher>>());
		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 1,
			Entries =
			[
				new VolunteerPointsEntryDto { TeamId = 42, TeamName = "10UB01 Sharks", Round = 1, Points = 2 }
			]
		};

		var result = await matcher.MatchEntriesAsync(request);

		result.Entries.Should().ContainSingle();
		result.Entries[0].TeamId.Should().Be(42);
	}

	[Fact]
	public async Task MatchEntriesAsync_LeavesUnmatchedNamesWithZeroTeamId()
	{
		var matcher = new VolunteerPointsTeamMatcher(Mock.Of<ILogger<VolunteerPointsTeamMatcher>>());
		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 1,
			Entries =
			[
				new VolunteerPointsEntryDto { TeamName = "Unknown Team", Round = 1, Points = 1 }
			]
		};

		var result = await matcher.MatchEntriesAsync(request);

		result.Entries.Should().ContainSingle();
		result.Entries[0].TeamId.Should().Be(0);
		result.Entries[0].TeamName.Should().Be("Unknown Team");
	}
}
