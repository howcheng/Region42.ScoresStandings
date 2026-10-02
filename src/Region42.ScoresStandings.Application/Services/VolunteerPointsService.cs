using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Exceptions;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Helpers;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for managing volunteer points that contribute to team standings.
/// Supports bulk entry via grid UI and point-in-time queries.
/// </summary>
public class VolunteerPointsService : IVolunteerPointsService
{
	private static readonly TimeSpan ImportLockTtl = TimeSpan.FromMinutes(15);

	private readonly IRepository<VolunteerPoints> _volunteerPointsRepository;
	private readonly IRepository<Team> _teamRepository;
	private readonly IRepository<Division> _divisionRepository;
	private readonly ISeasonService _seasonService;
	private readonly IGameService _gameService;
	private readonly IStandingsRefreshService _standingsRefreshService;
	private readonly IStorageWriteLock _storageWriteLock;
	private readonly ILogger<VolunteerPointsService> _logger;

	public VolunteerPointsService(
		IRepository<VolunteerPoints> volunteerPointsRepository,
		IRepository<Team> teamRepository,
		IRepository<Division> divisionRepository,
		ISeasonService seasonService,
		IGameService gameService,
		IStandingsRefreshService standingsRefreshService,
		IStorageWriteLock storageWriteLock,
		ILogger<VolunteerPointsService> logger)
	{
		_volunteerPointsRepository = volunteerPointsRepository;
		_teamRepository = teamRepository;
		_divisionRepository = divisionRepository;
		_seasonService = seasonService;
		_gameService = gameService;
		_standingsRefreshService = standingsRefreshService;
		_storageWriteLock = storageWriteLock;
		_logger = logger;
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByTeamAsync(int teamId)
	{
		_logger.LogInformation("Getting all volunteer points for team {TeamId}", teamId);

		var points = await _volunteerPointsRepository.FindAsync(vp => vp.TeamId == teamId);
		return points;
	}

	public async Task<VolunteerPoints?> GetVolunteerPointsByTeamAndRoundAsync(int teamId, int round)
	{
		_logger.LogInformation("Getting volunteer points for team {TeamId}, round {Round}", teamId, round);

		var points = await _volunteerPointsRepository.FindAsync(vp =>
			vp.TeamId == teamId && vp.Round == round);

		return points.FirstOrDefault();
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAsync(int divisionId)
	{
		_logger.LogInformation("Getting all volunteer points for division {DivisionId}", divisionId);

		var points = await _volunteerPointsRepository.FindAsync(vp =>
			vp.Team.DivisionId == divisionId);

		return points;
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAndRoundAsync(int divisionId, int throughRound)
	{
		_logger.LogInformation("Getting volunteer points for division {DivisionId} through round {Round}",
			divisionId, throughRound);

		var points = await _volunteerPointsRepository.FindAsync(vp =>
			vp.Team.DivisionId == divisionId && vp.Round <= throughRound);

		return points;
	}

	public async Task<VolunteerPoints> EnterOrUpdateVolunteerPointsAsync(int teamId, int round, decimal points, string notes)
	{
		_logger.LogInformation("Entering/updating volunteer points for team {TeamId}, round {Round}: Points={Points}",
			teamId, round, points);

		var team = await ValidateTeamForPointsAsync(teamId);
		ValidateRoundAndPoints(round, points, teamId);

		var existingPoints = await _volunteerPointsRepository.FindAsync(vp =>
			vp.TeamId == teamId && vp.Round == round);

		var existing = existingPoints.FirstOrDefault();

		if (existing != null)
		{
			_logger.LogInformation("Updating volunteer points for team {TeamId}, round {Round}. Old: {OldPoints}, New: {NewPoints}",
				teamId, round, existing.Points, points);

			existing.Points = points;
			existing.Notes = notes;

			_volunteerPointsRepository.Update(existing);
			await RefreshStandingsAndSaveAsync(team.DivisionId);
			return existing;
		}

		var newPoints = new VolunteerPoints
		{
			TeamId = teamId,
			Round = round,
			Points = points,
			Notes = notes
		};

		await _volunteerPointsRepository.AddAsync(newPoints);
		await RefreshStandingsAndSaveAsync(team.DivisionId);

		_logger.LogInformation("Created volunteer points entry for team {TeamId}, round {Round}", teamId, round);
		return newPoints;
	}

	public async Task<VolunteerPointsImportResultDto> BulkImportAsync(
		VolunteerPointsBulkUpdateDto request,
		bool dryRun,
		string importedBy,
		bool authoritativeSync = false,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(importedBy);

		var result = new VolunteerPointsImportResultDto { DryRun = dryRun };

		if (request.Entries == null || request.Entries.Count == 0)
		{
			result.ValidationErrors.Add("At least one volunteer points entry is required.");
			return result;
		}

		var division = await _divisionRepository.GetByIdAsync(request.DivisionId);
		if (division == null)
		{
			result.ValidationErrors.Add($"Division with ID {request.DivisionId} was not found.");
			return result;
		}

		IStorageWriteLockHandle? lockHandle = null;
		if (!dryRun)
		{
			lockHandle = await _storageWriteLock.TryAcquireAsync(
				StorageWriteLockNames.VolunteerPointsImport,
				importedBy,
				ImportLockTtl,
				cancellationToken);

			if (lockHandle == null)
			{
				throw new StorageWriteLockHeldException(StorageWriteLockNames.VolunteerPointsImport);
			}
		}

		try
		{
			var teamsInDivisionList = (await _teamRepository.FindAsync(t => t.DivisionId == request.DivisionId)).ToList();
			var teamsInDivision = teamsInDivisionList.ToDictionary(t => t.Id);

			var validEntries = new List<(VolunteerPointsEntryDto Entry, Team Team)>();
			var payloadKeys = new HashSet<(int TeamId, int Round)>();

			foreach (var entry in request.Entries)
			{
				var teamId = entry.TeamId;
				if (teamId <= 0 && !string.IsNullOrWhiteSpace(entry.TeamName))
				{
					var matched = VolunteerPointsTeamNameMatcher.TryMatch(entry.TeamName, teamsInDivisionList);
					if (matched != null)
					{
						teamId = matched.Id;
					}
				}

				if (teamId <= 0)
				{
					result.SkippedCount++;
					if (!string.IsNullOrWhiteSpace(entry.TeamName))
					{
						result.UnmatchedTeamNames.Add(entry.TeamName);
					}
					else
					{
						result.ValidationErrors.Add($"Entry for round {entry.Round} is missing a team identifier.");
					}

					continue;
				}

				if (!teamsInDivision.TryGetValue(teamId, out var team))
				{
					result.SkippedCount++;
					result.ValidationErrors.Add(
						$"Team {teamId} is not in division {request.DivisionId}.");
					continue;
				}

				if (!team.IsActive)
				{
					result.SkippedCount++;
					result.ValidationErrors.Add(
						$"Team {team.Name} (ID {team.Id}) is not active.");
					continue;
				}

				if (entry.Round < 1)
				{
					result.SkippedCount++;
					result.ValidationErrors.Add(
						$"Team {team.Name} round {entry.Round}: round must be greater than 0.");
					continue;
				}

				if (entry.Round > division.TotalRounds)
				{
					result.SkippedCount++;
					result.ValidationErrors.Add(
						$"Team {team.Name} round {entry.Round}: division only has {division.TotalRounds} rounds.");
					continue;
				}

				if (entry.Points < 0)
				{
					result.SkippedCount++;
					result.ValidationErrors.Add(
						$"Team {team.Name} round {entry.Round}: points cannot be negative.");
					continue;
				}

				payloadKeys.Add((teamId, entry.Round));
				validEntries.Add((new VolunteerPointsEntryDto
				{
					TeamId = teamId,
					TeamName = entry.TeamName,
					Round = entry.Round,
					Points = entry.Points,
					Notes = entry.Notes
				}, team));
			}

			var staleZeroEntries = new List<(int TeamId, int Round)>();
			if (authoritativeSync)
			{
				var existingInDivision = await GetVolunteerPointsByDivisionAsync(request.DivisionId);
				foreach (var existing in existingInDivision)
				{
					if (!IsSyncOwnedRecord(existing.ModifiedBy, importedBy))
					{
						continue;
					}

					var key = (existing.TeamId, existing.Round);
					if (!payloadKeys.Contains(key))
					{
						staleZeroEntries.Add(key);
					}
				}
			}

			if (dryRun)
			{
				result.ImportedCount = validEntries.Count;
				result.StaleZeroedCount = staleZeroEntries.Count;
				if (validEntries.Count > 0 || staleZeroEntries.Count > 0)
				{
					result.AffectedDivisionIds.Add(request.DivisionId);
				}

				return result;
			}

			foreach (var (entry, _) in validEntries)
			{
				var notes = string.IsNullOrWhiteSpace(entry.Notes)
					? $"Imported by {importedBy} on {DateTime.UtcNow:yyyy-MM-dd}"
					: entry.Notes;

				await UpsertVolunteerPointsWithoutSaveAsync(entry.TeamId, entry.Round, entry.Points, notes, importedBy);
				result.ImportedCount++;
			}

			foreach (var (teamId, round) in staleZeroEntries)
			{
				var notes = $"Cleared by authoritative sync ({importedBy}) on {DateTime.UtcNow:yyyy-MM-dd}";
				await UpsertVolunteerPointsWithoutSaveAsync(teamId, round, 0, notes, importedBy);
				result.StaleZeroedCount++;
			}

			if (result.ImportedCount > 0 || result.StaleZeroedCount > 0)
			{
				await _standingsRefreshService.RefreshDivisionStandingsAsync(request.DivisionId);
				await _volunteerPointsRepository.SaveChangesAsync();
				result.AffectedDivisionIds.Add(request.DivisionId);
			}

			_logger.LogInformation(
				"Bulk volunteer points import completed for division {DivisionId}: imported={Imported}, zeroed={Zeroed}, skipped={Skipped}, by={ImportedBy}",
				request.DivisionId,
				result.ImportedCount,
				result.StaleZeroedCount,
				result.SkippedCount,
				importedBy);

			return result;
		}
		finally
		{
			if (lockHandle != null)
			{
				await lockHandle.DisposeAsync();
			}
		}
	}

	public async Task<VolunteerPointsSyncContextDto> GetSyncContextAsync(CancellationToken cancellationToken = default)
	{
		var season = await _seasonService.GetActiveSeasonAsync();
		if (season == null)
		{
			return new VolunteerPointsSyncContextDto();
		}

		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == season.Id);
		var context = new VolunteerPointsSyncContextDto
		{
			SeasonId = season.Id,
			SeasonYear = season.Year
		};

		foreach (var division in divisions.OrderBy(d => d.AgeGroup).ThenBy(d => d.Gender))
		{
			var teams = (await _teamRepository.FindAsync(t => t.DivisionId == division.Id && t.IsActive))
				.OrderBy(t => t.Name)
				.ToList();

			var games = await _gameService.GetGamesByDivisionAsync(division.Id);
			var roundByDate = GameRoundCalculator.BuildRoundByDateMap(games.Select(g => g.ScheduledDateTime));

			context.Divisions.Add(new VolunteerPointsSyncDivisionDto
			{
				DivisionId = division.Id,
				CgiDivisionCode = DivisionKeyHelper.ToCgiSportsDivisionCode(division.AgeGroup, division.Gender),
				TotalRounds = division.TotalRounds,
				RoundByDate = new Dictionary<string, int>(roundByDate, StringComparer.Ordinal),
				Teams = teams.Select(t => new VolunteerPointsSyncTeamDto
				{
					TeamId = t.Id,
					Name = t.Name,
					ShortName = t.ShortName
				}).ToList()
			});
		}

		return context;
	}

	public Task<bool> IsVolunteerPointsImportLockedAsync(CancellationToken cancellationToken = default)
	{
		return _storageWriteLock.IsLockedAsync(StorageWriteLockNames.VolunteerPointsImport, cancellationToken);
	}

	public async Task<bool> DeleteVolunteerPointsAsync(int volunteerPointsId)
	{
		_logger.LogInformation("Deleting volunteer points {VolunteerPointsId}", volunteerPointsId);

		var points = await _volunteerPointsRepository.GetByIdAsync(volunteerPointsId);

		if (points == null)
		{
			_logger.LogWarning("Volunteer points {VolunteerPointsId} not found", volunteerPointsId);
			return false;
		}

		var team = await _teamRepository.GetByIdAsync(points.TeamId);
		_volunteerPointsRepository.Delete(points);
		if (team != null)
		{
			await RefreshStandingsAndSaveAsync(team.DivisionId);
		}
		else
		{
			await _volunteerPointsRepository.SaveChangesAsync();
		}

		_logger.LogInformation("Successfully deleted volunteer points {VolunteerPointsId}", volunteerPointsId);
		return true;
	}

	public async Task<bool> ValidateTeamAsync(int teamId)
	{
		_logger.LogDebug("Validating team {TeamId}", teamId);

		var team = await _teamRepository.GetByIdAsync(teamId);

		if (team == null)
		{
			_logger.LogDebug("Team {TeamId} not found", teamId);
			return false;
		}

		if (!team.IsActive)
		{
			_logger.LogDebug("Team {TeamId} is not active", teamId);
			return false;
		}

		return true;
	}

	private async Task UpsertVolunteerPointsWithoutSaveAsync(
		int teamId,
		int round,
		decimal points,
		string notes,
		string modifiedBy)
	{
		var existingPoints = await _volunteerPointsRepository.FindAsync(vp =>
			vp.TeamId == teamId && vp.Round == round);

		var existing = existingPoints.FirstOrDefault();
		if (existing != null)
		{
			existing.Points = points;
			existing.Notes = notes;
			existing.ModifiedBy = modifiedBy;
			existing.ModifiedAt = DateTime.UtcNow;
			_volunteerPointsRepository.Update(existing);
			return;
		}

		await _volunteerPointsRepository.AddAsync(new VolunteerPoints
		{
			TeamId = teamId,
			Round = round,
			Points = points,
			Notes = notes,
			ModifiedBy = modifiedBy,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = modifiedBy,
			CreatedAt = DateTime.UtcNow
		});
	}

	private static bool IsSyncOwnedRecord(string modifiedBy, string importedBy)
	{
		if (string.IsNullOrWhiteSpace(modifiedBy))
		{
			return false;
		}

		if (modifiedBy.Contains("volunteer-sync", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return string.Equals(modifiedBy, importedBy, StringComparison.OrdinalIgnoreCase);
	}

	private async Task RefreshStandingsAndSaveAsync(int divisionId)
	{
		await _standingsRefreshService.RefreshDivisionStandingsAsync(divisionId);
		await _volunteerPointsRepository.SaveChangesAsync();
	}

	private async Task<Team> ValidateTeamForPointsAsync(int teamId)
	{
		var team = await _teamRepository.GetByIdAsync(teamId);
		if (team == null)
		{
			_logger.LogWarning("Team {TeamId} not found", teamId);
			throw new ArgumentException($"Team with ID {teamId} not found", nameof(teamId));
		}

		if (!team.IsActive)
		{
			_logger.LogWarning("Team {TeamId} is not active", teamId);
			throw new InvalidOperationException($"Cannot assign volunteer points to inactive team {teamId}");
		}

		return team;
	}

	private void ValidateRoundAndPoints(int round, decimal points, int teamId)
	{
		if (round < 1)
		{
			_logger.LogWarning("Invalid round {Round} for team {TeamId}", round, teamId);
			throw new ArgumentException("Round must be greater than 0", nameof(round));
		}

		if (points < 0)
		{
			_logger.LogWarning("Invalid points {Points} for team {TeamId}, round {Round}", points, teamId, round);
			throw new ArgumentException("Points cannot be negative", nameof(points));
		}
	}
}
