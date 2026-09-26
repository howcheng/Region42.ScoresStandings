using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;

namespace Region42.ScoresStandings.VolunteerSync;

public class VolunteerSyncWorker : IHostedService
{
	private readonly VolunteerSyncOptions _options;
	private readonly IVolunteerPointsSourceClient _sourceClient;
	private readonly IVolunteerPointsFileParser _fileParser;
	private readonly IVolunteerPointsTeamMatcher _teamMatcher;
	private readonly IScoresStandingsApiClient _apiClient;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<VolunteerSyncWorker> _logger;

	public VolunteerSyncWorker(
		IOptions<VolunteerSyncOptions> options,
		IVolunteerPointsSourceClient sourceClient,
		IVolunteerPointsFileParser fileParser,
		IVolunteerPointsTeamMatcher teamMatcher,
		IScoresStandingsApiClient apiClient,
		IHostApplicationLifetime applicationLifetime,
		ILogger<VolunteerSyncWorker> logger)
	{
		_options = options.Value;
		_sourceClient = sourceClient;
		_fileParser = fileParser;
		_teamMatcher = teamMatcher;
		_apiClient = apiClient;
		_applicationLifetime = applicationLifetime;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_options.DivisionId <= 0)
			{
				throw new InvalidOperationException("VolunteerSync:DivisionId must be greater than zero.");
			}

			await using var fileStream = await _sourceClient.DownloadLatestFileAsync(cancellationToken);
			var parsed = _fileParser.Parse(fileStream, _options.DivisionId);
			var matched = await _teamMatcher.MatchEntriesAsync(parsed, cancellationToken);
			var result = await _apiClient.ImportVolunteerPointsAsync(matched, _options.DryRun, cancellationToken);

			_logger.LogInformation(
				"Volunteer points sync completed. DryRun={DryRun}, Imported={Imported}, Skipped={Skipped}, Unmatched={Unmatched}, ValidationErrors={ValidationErrors}",
				result.DryRun,
				result.ImportedCount,
				result.SkippedCount,
				result.UnmatchedTeamNames.Count,
				result.ValidationErrors.Count);

			if (result.ValidationErrors.Count > 0)
			{
				foreach (var error in result.ValidationErrors.Take(10))
				{
					_logger.LogWarning("Import validation error: {Error}", error);
				}
			}

			if (result.ImportedCount == 0 && result.SkippedCount > 0)
			{
				Environment.ExitCode = 2;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Volunteer points sync failed.");
			Environment.ExitCode = 1;
		}
		finally
		{
			_applicationLifetime.StopApplication();
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
