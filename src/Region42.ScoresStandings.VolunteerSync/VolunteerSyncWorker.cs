using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Services;

namespace Region42.ScoresStandings.VolunteerSync;

public class VolunteerSyncWorker : IHostedService
{
	private readonly VolunteerSyncOptions _options;
	private readonly IVolunteerPointsSourceClient _sourceClient;
	private readonly IVolunteerPointsFileParser _fileParser;
	private readonly VolunteerPointsImportPreparer _importPreparer;
	private readonly IScoresStandingsApiClient _apiClient;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly ILogger<VolunteerSyncWorker> _logger;

	public VolunteerSyncWorker(
		IOptions<VolunteerSyncOptions> options,
		IVolunteerPointsSourceClient sourceClient,
		IVolunteerPointsFileParser fileParser,
		VolunteerPointsImportPreparer importPreparer,
		IScoresStandingsApiClient apiClient,
		IHostApplicationLifetime applicationLifetime,
		ILogger<VolunteerSyncWorker> logger)
	{
		_options = options.Value;
		_sourceClient = sourceClient;
		_fileParser = fileParser;
		_importPreparer = importPreparer;
		_apiClient = apiClient;
		_applicationLifetime = applicationLifetime;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var fileStream = await _sourceClient.DownloadLatestFileAsync(cancellationToken);
			var rawRows = _fileParser.Parse(fileStream);
			_logger.LogInformation("Parsed {RowCount} aggregated volunteer point rows from export", rawRows.Count);

			var syncContext = await _apiClient.GetSyncContextAsync(cancellationToken);
			if (syncContext.SeasonId <= 0)
			{
				throw new InvalidOperationException("No active season is configured for volunteer points sync.");
			}

			var divisionImports = _importPreparer.BuildDivisionImports(rawRows, syncContext);
			if (_options.DivisionId > 0)
			{
				divisionImports = divisionImports.Where(d => d.DivisionId == _options.DivisionId).ToList();
			}

			if (divisionImports.Count == 0)
			{
				_logger.LogWarning("No volunteer points imports were prepared from the export.");
				Environment.ExitCode = 2;
				return;
			}

			var totalImported = 0;
			var totalSkipped = 0;
			var totalUnmatched = 0;
			var anyFailure = false;

			foreach (var import in divisionImports)
			{
				var result = await _apiClient.ImportVolunteerPointsAsync(
					import,
					_options.DryRun,
					authoritativeSync: true,
					cancellationToken);

				totalImported += result.ImportedCount;
				totalSkipped += result.SkippedCount;
				totalUnmatched += result.UnmatchedTeamNames.Count;

				_logger.LogInformation(
					"Division {DivisionId} sync result: DryRun={DryRun}, Imported={Imported}, Zeroed={Zeroed}, Skipped={Skipped}, Unmatched={Unmatched}",
					import.DivisionId,
					result.DryRun,
					result.ImportedCount,
					result.StaleZeroedCount,
					result.SkippedCount,
					result.UnmatchedTeamNames.Count);

				if (result.UnmatchedTeamNames.Count > 0)
				{
					foreach (var teamName in result.UnmatchedTeamNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
					{
						if (VolunteerPointsTeamNameMatcher.IsLikelyCoachNameOnly(teamName))
						{
							_logger.LogWarning(
								"Division {DivisionId} skipped coach-name-only label (not auto-matched): {TeamName}",
								import.DivisionId,
								teamName);
						}
						else
						{
							_logger.LogWarning(
								"Division {DivisionId} could not match team label from export: {TeamName}",
								import.DivisionId,
								teamName);
						}
					}
				}

				if (result.ValidationErrors.Count > 0)
				{
					foreach (var error in result.ValidationErrors.Take(10))
					{
						_logger.LogWarning("Division {DivisionId} validation error: {Error}", import.DivisionId, error);
					}
				}

				if (result.ImportedCount == 0 && result.SkippedCount > 0)
				{
					anyFailure = true;
				}
			}

			_logger.LogInformation(
				"Volunteer points sync completed. DryRun={DryRun}, Divisions={DivisionCount}, Imported={Imported}, Skipped={Skipped}, Unmatched={Unmatched}",
				_options.DryRun,
				divisionImports.Count,
				totalImported,
				totalSkipped,
				totalUnmatched);

			if (anyFailure)
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
