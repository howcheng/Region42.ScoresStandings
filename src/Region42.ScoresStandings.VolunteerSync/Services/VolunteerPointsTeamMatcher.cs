using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Services;

/// <summary>
/// Maps external team names from the volunteer points export to internal team identifiers.
/// </summary>
public class VolunteerPointsTeamMatcher : IVolunteerPointsTeamMatcher
{
	private readonly ILogger<VolunteerPointsTeamMatcher> _logger;

	public VolunteerPointsTeamMatcher(ILogger<VolunteerPointsTeamMatcher> logger)
	{
		_logger = logger;
	}

	public Task<VolunteerPointsBulkUpdateDto> MatchEntriesAsync(
		VolunteerPointsBulkUpdateDto parsedRequest,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(parsedRequest);

		var matched = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = parsedRequest.DivisionId,
			Entries = new List<VolunteerPointsEntryDto>()
		};

		foreach (var entry in parsedRequest.Entries)
		{
			if (entry.TeamId > 0)
			{
				matched.Entries.Add(entry);
				continue;
			}

			var teamId = TryMatchTeamId(entry.TeamName);
			if (teamId.HasValue)
			{
				matched.Entries.Add(new VolunteerPointsEntryDto
				{
					TeamId = teamId.Value,
					TeamName = entry.TeamName,
					Round = entry.Round,
					Points = entry.Points,
					Notes = entry.Notes
				});
				continue;
			}

			_logger.LogWarning("Unable to match volunteer points team name '{TeamName}'", entry.TeamName);
			matched.Entries.Add(new VolunteerPointsEntryDto
			{
				TeamId = 0,
				TeamName = entry.TeamName,
				Round = entry.Round,
				Points = entry.Points,
				Notes = entry.Notes
			});
		}

		return Task.FromResult(matched);
	}

	private static int? TryMatchTeamId(string teamName)
	{
		if (string.IsNullOrWhiteSpace(teamName))
		{
			return null;
		}

		// TODO: Replace heuristic with lookup against ScoresStandings team metadata once source format is known.
		return null;
	}
}
