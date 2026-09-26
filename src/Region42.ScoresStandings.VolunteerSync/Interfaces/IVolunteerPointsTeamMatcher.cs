using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IVolunteerPointsTeamMatcher
{
	Task<VolunteerPointsBulkUpdateDto> MatchEntriesAsync(
		VolunteerPointsBulkUpdateDto parsedRequest,
		CancellationToken cancellationToken = default);
}
