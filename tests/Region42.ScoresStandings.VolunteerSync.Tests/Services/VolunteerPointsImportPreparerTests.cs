using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.VolunteerSync.Models;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync.Tests.Services;

public class VolunteerPointsImportPreparerTests
{
	private readonly VolunteerPointsImportPreparer _preparer;

	public VolunteerPointsImportPreparerTests()
	{
		_preparer = new VolunteerPointsImportPreparer(Mock.Of<ILogger<VolunteerPointsImportPreparer>>());
	}

	[Fact]
	public void BuildDivisionImports_WhenMaxVolunteerPointsPerSeasonSet_CapsCumulativePointsAndZeroesSubsequentRounds()
	{
		var rawRows = new List<VolunteerPointsRawRow>
		{
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 5), Points = 2m },
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 12), Points = 2.5m },
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 19), Points = 2.5m },
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 26), Points = 2m },
			new() { DivisionCode = "10UB", Team = "10UB02 (Jones)", Date = new DateTime(2026, 9, 5), Points = 2m },
			new() { DivisionCode = "10UB", Team = "10UB02 (Jones)", Date = new DateTime(2026, 9, 12), Points = 1.5m }
		};

		var syncContext = new VolunteerPointsSyncContextDto
		{
			SeasonId = 1,
			SeasonYear = 2026,
			MaxVolunteerPointsPerSeason = 6m,
			Divisions =
			[
				new VolunteerPointsSyncDivisionDto
				{
					DivisionId = 10,
					CgiDivisionCode = "10UB",
					TotalRounds = 10,
					RoundByDate = new Dictionary<string, int>(StringComparer.Ordinal)
					{
						["2026-09-05"] = 1,
						["2026-09-12"] = 2,
						["2026-09-19"] = 3,
						["2026-09-26"] = 4
					}
				}
			]
		};

		var imports = _preparer.BuildDivisionImports(rawRows, syncContext);

		var divisionImport = imports.Should().ContainSingle(d => d.DivisionId == 10).Subject;

		var team1Entries = divisionImport.Entries
			.Where(e => e.TeamName == "10UB01 (Smith)")
			.OrderBy(e => e.Round)
			.ToList();

		team1Entries.Should().HaveCount(4);
		team1Entries[0].Should().BeEquivalentTo(new { Round = 1, Points = 2m });
		team1Entries[1].Should().BeEquivalentTo(new { Round = 2, Points = 2.5m });
		team1Entries[2].Should().BeEquivalentTo(new { Round = 3, Points = 1.5m });
		team1Entries[3].Should().BeEquivalentTo(new { Round = 4, Points = 0m });
		team1Entries.Sum(e => e.Points).Should().Be(6m);

		var team2Entries = divisionImport.Entries
			.Where(e => e.TeamName == "10UB02 (Jones)")
			.OrderBy(e => e.Round)
			.ToList();

		team2Entries.Should().HaveCount(2);
		team2Entries[0].Should().BeEquivalentTo(new { Round = 1, Points = 2m });
		team2Entries[1].Should().BeEquivalentTo(new { Round = 2, Points = 1.5m });
		team2Entries.Sum(e => e.Points).Should().Be(3.5m);
	}

	[Fact]
	public void BuildDivisionImports_WhenMaxVolunteerPointsPerSeasonIsZero_DoesNotCapPoints()
	{
		var rawRows = new List<VolunteerPointsRawRow>
		{
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 5), Points = 4m },
			new() { DivisionCode = "10UB", Team = "10UB01 (Smith)", Date = new DateTime(2026, 9, 12), Points = 4m }
		};

		var syncContext = new VolunteerPointsSyncContextDto
		{
			SeasonId = 1,
			SeasonYear = 2026,
			MaxVolunteerPointsPerSeason = 0m,
			Divisions =
			[
				new VolunteerPointsSyncDivisionDto
				{
					DivisionId = 10,
					CgiDivisionCode = "10UB",
					TotalRounds = 10,
					RoundByDate = new Dictionary<string, int>(StringComparer.Ordinal)
					{
						["2026-09-05"] = 1,
						["2026-09-12"] = 2
					}
				}
			]
		};

		var imports = _preparer.BuildDivisionImports(rawRows, syncContext);

		var divisionImport = imports.Should().ContainSingle().Subject;
		divisionImport.Entries.Sum(e => e.Points).Should().Be(8m);
	}

	[Fact]
	public void BuildDivisionImports_WhenPointsEarnedOnMidWeekAndSundayDates_AllocatesToWeekRound()
	{
		var rawRows = new List<VolunteerPointsRawRow>
		{
			new() { DivisionCode = "14UG", Team = "14UG01 (Clark)", Date = new DateTime(2026, 10, 20), Points = 1m },
			new() { DivisionCode = "14UG", Team = "14UG01 (Clark)", Date = new DateTime(2026, 10, 24), Points = 2m },
			new() { DivisionCode = "14UG", Team = "14UG02 (Curtis)", Date = new DateTime(2026, 10, 25), Points = 2m }
		};

		var syncContext = new VolunteerPointsSyncContextDto
		{
			SeasonId = 3,
			SeasonYear = 2026,
			MaxVolunteerPointsPerSeason = 14m,
			Divisions =
			[
				new VolunteerPointsSyncDivisionDto
				{
					DivisionId = 24,
					CgiDivisionCode = "14UG",
					TotalRounds = 10,
					RoundByDate = new Dictionary<string, int>(StringComparer.Ordinal)
					{
						["2026-10-17"] = 7,
						["2026-10-20"] = 8,
						["2026-10-24"] = 8,
						["2026-10-25"] = 9,
						["2026-10-31"] = 9
					}
				}
			]
		};

		var imports = _preparer.BuildDivisionImports(rawRows, syncContext);

		var divisionImport = imports.Should().ContainSingle(d => d.DivisionId == 24).Subject;
		divisionImport.Entries.Should().ContainSingle(e =>
			e.TeamName == "14UG01 (Clark)" && e.Round == 8 && e.Points == 3m);
		divisionImport.Entries.Should().ContainSingle(e =>
			e.TeamName == "14UG02 (Curtis)" && e.Round == 9 && e.Points == 2m);
	}
}
