namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IVolunteerPointsSourceClient
{
	Task<Stream> DownloadLatestFileAsync(CancellationToken cancellationToken = default);
}
