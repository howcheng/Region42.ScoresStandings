using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IScoresStandingsApiClient
{
	Task<VolunteerPointsImportResultDto> ImportVolunteerPointsAsync(
		VolunteerPointsBulkUpdateDto request,
		bool dryRun,
		CancellationToken cancellationToken = default);
}
