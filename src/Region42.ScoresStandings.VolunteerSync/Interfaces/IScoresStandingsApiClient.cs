using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IScoresStandingsApiClient
{
	Task<VolunteerPointsSyncContextDto> GetSyncContextAsync(CancellationToken cancellationToken = default);

	Task<VolunteerPointsImportResultDto> ImportVolunteerPointsAsync(
		VolunteerPointsBulkUpdateDto request,
		bool dryRun,
		bool authoritativeSync = false,
		CancellationToken cancellationToken = default);
}
