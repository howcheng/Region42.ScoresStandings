using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class ScoresController : Controller
{
	private readonly IScoreService _scoreService;
	private readonly IGameService _gameService;
	private readonly ITeamService _teamService;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Season> _seasonRepository;
	private readonly IRepository<Score> _scoreRepository;
	private readonly ILogger<ScoresController> _logger;
	private readonly ITournamentService? _tournamentService;
	private readonly IStandingsRefreshService? _standingsRefreshService;

	public ScoresController(
		IScoreService scoreService,
		IGameService gameService,
		ITeamService teamService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository,
		IRepository<Score> scoreRepository,
		ILogger<ScoresController> logger,
		ITournamentService? tournamentService = null,
		IStandingsRefreshService? standingsRefreshService = null)
	{
		_scoreService = scoreService;
		_gameService = gameService;
		_teamService = teamService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
		_scoreRepository = scoreRepository;
		_logger = logger;
		_tournamentService = tournamentService;
		_standingsRefreshService = standingsRefreshService;
	}

	// GET: Scores/Entry
	public async Task<IActionResult> Entry(int? divisionId, int? round)
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

		if (currentSeason == null)
		{
			ViewBag.ErrorMessage = "No active season found.";
			return View(new List<ScoreEntryDto>());
		}

		var divisions = (await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id)).ToList();
		var divisionList = divisions.Select(d => new
		{
			Id = d.Id,
			Name = $"{d.AgeGroup} {d.Gender}",
			TotalRounds = d.TotalRounds,
			EndOfSeasonTournamentFormat = d.EndOfSeasonTournamentFormat
		}).ToList();

		ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", divisionId);

		if (!divisionId.HasValue)
		{
			ViewBag.Rounds = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
			return View(new List<ScoreEntryDto>());
		}

		var selectedDivision = divisionList.FirstOrDefault(d => d.Id == divisionId.Value);
		if (selectedDivision == null)
		{
			return NotFound();
		}

		var hasTournament = selectedDivision.EndOfSeasonTournamentFormat != TournamentFormatType.None;
		var rounds = Enumerable.Range(1, selectedDivision.TotalRounds)
			.Select(r => new SelectListItem
			{
				Value = r.ToString(),
				Text = hasTournament && r == selectedDivision.TotalRounds
					? $"Round {r} (Tournament)"
					: $"Round {r}"
			});
		ViewBag.Rounds = new SelectList(rounds, "Value", "Text", round);
		ViewBag.SelectedDivisionId = divisionId;

		if (!round.HasValue)
		{
			return View(new List<ScoreEntryDto>());
		}

		var isTournamentRound = hasTournament && round.Value == selectedDivision.TotalRounds;
		ViewBag.IsTournamentRound = isTournamentRound;
		ViewBag.TournamentFormat = selectedDivision.EndOfSeasonTournamentFormat;

		if (isTournamentRound && _tournamentService != null)
		{
			try
			{
				await _tournamentService.EnsureTournamentGamesAsync(divisionId.Value);
				ViewBag.TournamentDisplay = await _tournamentService.GetTournamentBracketAsync(divisionId.Value);
			}
			catch (NotSupportedException ex)
			{
				ViewBag.ErrorMessage = ex.Message;
			}
		}

		var games = (await _gameService.GetGamesByDivisionAndRoundAsync(divisionId.Value, round.Value)).ToList();

		var teams = await _teamService.GetTeamsByDivisionAsync(divisionId.Value);
		var teamList = teams.Select(t => new SelectListItem
		{
			Value = t.Id.ToString(),
			Text = t.Name
		}).ToList();
		ViewBag.Teams = teamList;

		var scoreEntries = new List<ScoreEntryDto>();
		var orderedGames = games
			.OrderBy(g => g is TournamentGame tg ? (int)tg.Stage : 0)
			.ThenBy(g => g.ScheduledDateTime)
			.ToList();

		foreach (var game in orderedGames)
		{
			var score = await _scoreService.GetScoreByGameIdAsync(game.Id);
			Game? makeupGame = null;
			if (game.RescheduledToGameId.HasValue)
			{
				makeupGame = await _gameService.GetGameByIdAsync(game.RescheduledToGameId.Value);
			}

			scoreEntries.Add(BuildScoreEntryDto(game, score, makeupGame));
		}

		ViewBag.SelectedRound = round;
		ViewBag.HasEnteredTournamentScores = isTournamentRound && scoreEntries.Any(s => s.HomeScore.HasValue || s.AwayScore.HasValue);

		return View(scoreEntries);
	}

	// POST: Scores/Entry
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Entry(
		List<ScoreUpdateDto> scores,
		int divisionId,
		int round,
		int? poolPlayTiebreakerWinnerTeamId = null)
	{
		if (scores == null || !scores.Any())
		{
			TempData["ErrorMessage"] = "No scores to save.";
			return RedirectToAction(nameof(Entry), new { divisionId, round });
		}

		var batchGamesById = new Dictionary<int, Game>();
		foreach (var score in scores)
		{
			var existingGame = await _gameService.GetGameByIdAsync(score.GameId);
			if (existingGame != null)
			{
				batchGamesById[score.GameId] = existingGame;
			}

			var isUnassignedTournamentMatch = existingGame is TournamentGame &&
				(score.HomeTeamId == 0 || score.AwayTeamId == 0);

			if (!isUnassignedTournamentMatch && score.HomeTeamId == score.AwayTeamId)
			{
				TempData["ErrorMessage"] = "A team cannot play against itself. Please check the schedule.";
				return RedirectToAction(nameof(Entry), new { divisionId, round });
			}
		}

		var batchGameIds = scores.Select(s => s.GameId).ToHashSet();
		var roundTeamAppearances = new Dictionary<int, int>();

		foreach (var score in scores)
		{
			if (score.Status == GameStatus.Rescheduled)
			{
				continue;
			}

			if (batchGamesById.TryGetValue(score.GameId, out var batchGame) &&
				(batchGame.RescheduledFromGameId.HasValue || batchGame is TournamentGame))
			{
				continue;
			}

			roundTeamAppearances[score.HomeTeamId] = roundTeamAppearances.GetValueOrDefault(score.HomeTeamId) + 1;
			roundTeamAppearances[score.AwayTeamId] = roundTeamAppearances.GetValueOrDefault(score.AwayTeamId) + 1;
		}

		var existingGamesInRound = await _gameService.GetGamesByDivisionAndRoundAsync(divisionId, round);
		foreach (var existingGame in existingGamesInRound.Where(g =>
			!batchGameIds.Contains(g.Id) &&
			g is not TournamentGame &&
			g.Status != GameStatus.Rescheduled &&
			!g.RescheduledFromGameId.HasValue))
		{
			roundTeamAppearances[existingGame.HomeTeamId] = roundTeamAppearances.GetValueOrDefault(existingGame.HomeTeamId) + 1;
			roundTeamAppearances[existingGame.AwayTeamId] = roundTeamAppearances.GetValueOrDefault(existingGame.AwayTeamId) + 1;
		}

		var duplicateTeams = roundTeamAppearances.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key).ToList();
		if (duplicateTeams.Any())
		{
			var teams = await _teamService.GetTeamsByDivisionAsync(divisionId);
			var teamNames = teams.Where(t => duplicateTeams.Contains(t.Id))
				.Select(t => t.Name)
				.ToList();
			TempData["ErrorMessage"] = $"The following team(s) would appear more than once in round {round}: {string.Join(", ", teamNames)}. Each team can only have one game per round.";
			return RedirectToAction(nameof(Entry), new { divisionId, round });
		}

		var successCount = 0;
		var errorCount = 0;
		var errors = new List<string>();
		var hasTournamentGames = false;

		foreach (var scoreUpdate in scores)
		{
			try
			{
				var game = await _gameService.GetGameByIdAsync(scoreUpdate.GameId);
				if (game == null)
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId} not found.");
					continue;
				}

				if (game is TournamentGame)
				{
					hasTournamentGames = true;
				}

				var hasHomeScore = scoreUpdate.HomeScore.HasValue;
				var hasAwayScore = scoreUpdate.AwayScore.HasValue;
				if (hasHomeScore != hasAwayScore)
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId}: Both home and away scores must be entered. A game is not complete until both scores are added.");
					_logger.LogWarning("Partial score entry attempted for game {GameId}", scoreUpdate.GameId);
					continue;
				}

				if (scoreUpdate.Status == GameStatus.Rescheduled && !scoreUpdate.NewScheduledDateTime.HasValue)
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId}: A new date/time is required when marking a game as Rescheduled.");
					_logger.LogWarning("Rescheduled game {GameId} missing new date/time", scoreUpdate.GameId);
					continue;
				}

				var wasUnscheduled = game.Status == GameStatus.Unscheduled;
				var wasRescheduled = game.Status == GameStatus.Rescheduled;
				if (wasUnscheduled &&
					scoreUpdate.Status == GameStatus.Scheduled &&
					!scoreUpdate.NewScheduledDateTime.HasValue &&
					!(hasHomeScore && hasAwayScore))
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId}: A date/time is required when marking an unscheduled game as Scheduled.");
					_logger.LogWarning("Unscheduled game {GameId} marked Scheduled without date/time", scoreUpdate.GameId);
					continue;
				}

				bool gameChanged = false;
				if (scoreUpdate.HomeTeamId > 0 && scoreUpdate.AwayTeamId > 0 &&
					(game.HomeTeamId != scoreUpdate.HomeTeamId || game.AwayTeamId != scoreUpdate.AwayTeamId))
				{
					game.HomeTeamId = scoreUpdate.HomeTeamId;
					game.AwayTeamId = scoreUpdate.AwayTeamId;
					gameChanged = true;
				}

				if (game.Status != scoreUpdate.Status)
				{
					game.Status = scoreUpdate.Status;
					gameChanged = true;
					_logger.LogInformation("Updated status for game {GameId} to {Status}", scoreUpdate.GameId, scoreUpdate.Status);
				}

				if (scoreUpdate.Status != GameStatus.Rescheduled)
				{
					DateTime? oldMakeupDateTime = null;
					string? oldMakeupLocation = null;

					if (game.RescheduledToGameId.HasValue)
					{
						var oldMakeupId = game.RescheduledToGameId.Value;
						var oldMakeup = await _gameService.GetGameByIdAsync(oldMakeupId);
						if (oldMakeup != null)
						{
							oldMakeupDateTime = oldMakeup.ScheduledDateTime;
							oldMakeupLocation = oldMakeup.Location;
							if (oldMakeup.Score == null)
							{
								await _gameService.DeleteGameAsync(oldMakeupId, saveChanges: false);
							}
						}
						game.RescheduledToGameId = null;
						gameChanged = true;
					}

					if (scoreUpdate.NewScheduledDateTime.HasValue)
					{
						var newUtcDateTime = TimezoneHelper.ToUtc(scoreUpdate.NewScheduledDateTime.Value);
						var existingUtcMinute = game.ScheduledDateTime == default
							? default
							: new DateTime(
								game.ScheduledDateTime.Year,
								game.ScheduledDateTime.Month,
								game.ScheduledDateTime.Day,
								game.ScheduledDateTime.Hour,
								game.ScheduledDateTime.Minute,
								0,
								DateTimeKind.Utc);

						if (!wasUnscheduled && scoreUpdate.Status == GameStatus.Unscheduled && existingUtcMinute == newUtcDateTime)
						{
							game.ScheduledDateTime = default;
							gameChanged = true;
						}
						else if (!(wasRescheduled && oldMakeupDateTime == newUtcDateTime) && existingUtcMinute != newUtcDateTime)
						{
							game.ScheduledDateTime = newUtcDateTime;
							if (game.Status == GameStatus.Unscheduled)
							{
								game.Status = GameStatus.Scheduled;
							}
							gameChanged = true;
							_logger.LogInformation("Scheduled game {GameId} on {ScheduledDateTime}",
								scoreUpdate.GameId, game.ScheduledDateTime);
						}
					}
					else if (scoreUpdate.Status == GameStatus.Unscheduled && game.ScheduledDateTime != default)
					{
						game.ScheduledDateTime = default;
						gameChanged = true;
					}

					if (scoreUpdate.NewLocation != null)
					{
						var trimmedLocation = scoreUpdate.NewLocation.Trim();
						if (!(wasRescheduled && oldMakeupLocation == trimmedLocation) && game.Location != trimmedLocation)
						{
							game.Location = trimmedLocation;
							gameChanged = true;
							_logger.LogInformation("Updated location for game {GameId} to {Location}",
								scoreUpdate.GameId, game.Location);
						}
					}
				}
				else
				{
					var newUtcDateTime = TimezoneHelper.ToUtc(scoreUpdate.NewScheduledDateTime!.Value);
					var newLocation = !string.IsNullOrWhiteSpace(scoreUpdate.NewLocation)
						? scoreUpdate.NewLocation.Trim()
						: game.Location;

					int targetRound;
					if (scoreUpdate.NewRound.HasValue)
					{
						targetRound = scoreUpdate.NewRound.Value;
					}
					else
					{
						var divisionGames = (await _gameService.GetGamesByDivisionAsync(divisionId))?.ToList() ?? new List<Game>();
						var roundByDate = divisionGames
							.Where(g => !g.RescheduledFromGameId.HasValue && g.Status != GameStatus.Unscheduled && g.ScheduledDateTime != default)
							.GroupBy(g => g.ScheduledDateTime.Date.ToString("yyyy-MM-dd"))
							.ToDictionary(grp => grp.Key, grp => grp.First().Round, StringComparer.Ordinal);
						targetRound = GameRoundCalculator.GetRoundForDate(newUtcDateTime, roundByDate) ?? game.Round;
					}

					Game? makeupGame = null;
					if (game.RescheduledToGameId.HasValue)
					{
						makeupGame = await _gameService.GetGameByIdAsync(game.RescheduledToGameId.Value);
					}

					if (makeupGame != null)
					{
						if (makeupGame.HomeTeamId != game.HomeTeamId ||
							makeupGame.AwayTeamId != game.AwayTeamId ||
							makeupGame.ScheduledDateTime != newUtcDateTime ||
							makeupGame.Location != newLocation ||
							makeupGame.Round != targetRound ||
							makeupGame.RescheduledFromGameId != game.Id)
						{
							makeupGame.HomeTeamId = game.HomeTeamId;
							makeupGame.AwayTeamId = game.AwayTeamId;
							makeupGame.ScheduledDateTime = newUtcDateTime;
							makeupGame.Location = newLocation;
							makeupGame.Round = targetRound;
							makeupGame.RescheduledFromGameId = game.Id;
							await _gameService.UpdateGameAsync(makeupGame, saveChanges: false);
							gameChanged = true;
						}
					}
					else
					{
						var createdMakeup = await _gameService.CreateGameAsync(new Game
						{
							DivisionId = game.DivisionId,
							HomeTeamId = game.HomeTeamId,
							AwayTeamId = game.AwayTeamId,
							ScheduledDateTime = newUtcDateTime,
							Location = newLocation,
							Round = targetRound,
							Status = GameStatus.Scheduled,
							RescheduledFromGameId = game.Id
						}, saveChanges: false);
						game.RescheduledToGameId = createdMakeup.Id;
						gameChanged = true;
					}

					if (gameChanged)
					{
						_logger.LogInformation("Rescheduled game {GameId} to round {TargetRound} on {ScheduledDateTime} at {Location}",
							scoreUpdate.GameId, targetRound, newUtcDateTime, newLocation);
					}
				}

				if (gameChanged)
				{
					await _gameService.UpdateGameAsync(game, saveChanges: false);
					_logger.LogInformation("Updated game {GameId}: Home={HomeTeamId}, Away={AwayTeamId}",
						scoreUpdate.GameId, scoreUpdate.HomeTeamId, scoreUpdate.AwayTeamId);
				}

				var scoreChanged = false;
				var scoreCleared = false;
				if (hasHomeScore && hasAwayScore)
				{
					if (HasScoreChanged(game, scoreUpdate))
					{
						if (game is PoolPlayGame)
						{
							await _scoreService.EnterOrUpdatePoolPlayScoreAsync(
								scoreUpdate.GameId,
								scoreUpdate.HomeScore!.Value,
								scoreUpdate.AwayScore!.Value,
								scoreUpdate.IsForfeit,
								scoreUpdate.HomeRedCards,
								scoreUpdate.AwayRedCards,
								scoreUpdate.HomeYellowCards,
								scoreUpdate.AwayYellowCards,
								saveChanges: false);
						}
						else if (game is EliminationGame)
						{
							await _scoreService.EnterOrUpdateEliminationScoreAsync(
								scoreUpdate.GameId,
								scoreUpdate.HomeScore!.Value,
								scoreUpdate.AwayScore!.Value,
								scoreUpdate.IsForfeit,
								scoreUpdate.IsDecidedByPenalties,
								scoreUpdate.HomePenaltyScore,
								scoreUpdate.AwayPenaltyScore,
								saveChanges: false);
						}
						else
						{
							await _scoreService.EnterOrUpdateScoreAsync(
								scoreUpdate.GameId,
								scoreUpdate.HomeScore!.Value,
								scoreUpdate.AwayScore!.Value,
								saveChanges: false);
						}

						scoreChanged = true;
						_logger.LogInformation("Score saved for game {GameId}: {HomeScore}-{AwayScore}",
							scoreUpdate.GameId, scoreUpdate.HomeScore, scoreUpdate.AwayScore);
					}
				}
				else if (game.Score?.HomeScore != null || game.Score?.AwayScore != null)
				{
					scoreCleared = await _scoreService.DeleteScoreAsync(scoreUpdate.GameId, saveChanges: false);
					if (scoreCleared)
					{
						_logger.LogInformation("Cleared score for game {GameId}", scoreUpdate.GameId);
					}
				}

				if (gameChanged || scoreChanged || scoreCleared)
				{
					successCount++;
				}
			}
			catch (Exception ex)
			{
				errorCount++;
				errors.Add($"Game {scoreUpdate.GameId}: {ex.Message}");
				_logger.LogError(ex, "Error updating game/score {GameId}", scoreUpdate.GameId);
			}
		}

		try
		{
			if (poolPlayTiebreakerWinnerTeamId.HasValue)
			{
				var division = await _divisionRepository.GetByIdAsync(divisionId);
				if (division != null)
				{
					var newTiebreakerId = poolPlayTiebreakerWinnerTeamId.Value > 0 ? poolPlayTiebreakerWinnerTeamId.Value : (int?)null;
					if (division.PoolPlayTiebreakerWinnerTeamId != newTiebreakerId)
					{
						division.PoolPlayTiebreakerWinnerTeamId = newTiebreakerId;
						_divisionRepository.Update(division);
						hasTournamentGames = true;
						successCount++;
					}
				}
			}

			if (successCount > 0)
			{
				if (hasTournamentGames && _tournamentService != null)
				{
					await _tournamentService.SyncTournamentProgressionAsync(divisionId, saveChanges: false);
				}

				if (_standingsRefreshService != null)
				{
					await _standingsRefreshService.RefreshDivisionStandingsAsync(divisionId);
				}

				await _scoreRepository.SaveChangesAsync();
			}
		}
		catch (Exception ex)
		{
			errorCount++;
			errors.Add($"Failed to persist changes: {ex.Message}");
			successCount = 0;
			_logger.LogError(ex, "Error persisting batch updates for division {DivisionId}, round {Round}", divisionId, round);
		}

		if (successCount > 0)
		{
			TempData["SuccessMessage"] = $"{successCount} game(s) saved successfully.";
		}
		else if (errorCount == 0)
		{
			TempData["SuccessMessage"] = "No changes to save.";
		}

		if (errorCount > 0)
		{
			TempData["ErrorMessage"] = $"{errorCount} game(s) failed to save. Errors: {string.Join("; ", errors)}";
		}

		return RedirectToAction(nameof(Entry), new { divisionId, round });
	}

	private static bool HasScoreChanged(Game game, ScoreUpdateDto scoreUpdate)
	{
		if (!scoreUpdate.HomeScore.HasValue || !scoreUpdate.AwayScore.HasValue)
		{
			return false;
		}

		var existingScore = game.Score;
		if (existingScore == null)
		{
			return true;
		}

		if (existingScore.HomeScore != scoreUpdate.HomeScore.Value ||
			existingScore.AwayScore != scoreUpdate.AwayScore.Value)
		{
			return true;
		}

		if (game is PoolPlayGame)
		{
			if (existingScore is not PoolPlayScore existingPoolScore)
			{
				return true;
			}

			return existingPoolScore.IsForfeit != scoreUpdate.IsForfeit ||
				existingPoolScore.HomeRedCards != scoreUpdate.HomeRedCards ||
				existingPoolScore.AwayRedCards != scoreUpdate.AwayRedCards ||
				existingPoolScore.HomeYellowCards != scoreUpdate.HomeYellowCards ||
				existingPoolScore.AwayYellowCards != scoreUpdate.AwayYellowCards;
		}

		if (game is EliminationGame)
		{
			if (existingScore is not EliminationScore existingElimScore)
			{
				return true;
			}

			var decidedByPenalties = !scoreUpdate.IsForfeit &&
				scoreUpdate.HomeScore.Value == scoreUpdate.AwayScore.Value &&
				scoreUpdate.HomePenaltyScore.HasValue &&
				scoreUpdate.AwayPenaltyScore.HasValue;
			var rawPkEntered = scoreUpdate.IsDecidedByPenalties ||
				scoreUpdate.HomePenaltyScore.HasValue ||
				scoreUpdate.AwayPenaltyScore.HasValue;
			if (!decidedByPenalties && rawPkEntered)
			{
				return true;
			}

			var expectedHomePk = decidedByPenalties ? scoreUpdate.HomePenaltyScore : null;
			var expectedAwayPk = decidedByPenalties ? scoreUpdate.AwayPenaltyScore : null;

			return existingElimScore.IsForfeit != scoreUpdate.IsForfeit ||
				existingElimScore.IsDecidedByPenalties != decidedByPenalties ||
				existingElimScore.HomePenaltyScore != expectedHomePk ||
				existingElimScore.AwayPenaltyScore != expectedAwayPk;
		}

		return false;
	}

	// POST: Scores/RegenerateTournament
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> RegenerateTournament(int divisionId, int round, bool forceReset = false)
	{
		if (_tournamentService == null)
		{
			TempData["ErrorMessage"] = "Tournament service is not available.";
			return RedirectToAction(nameof(Entry), new { divisionId, round });
		}

		try
		{
			await _tournamentService.GenerateOrResetTournamentGamesAsync(divisionId, forceReset, saveChanges: false);
			if (_standingsRefreshService != null)
			{
				await _standingsRefreshService.RefreshDivisionStandingsAsync(divisionId);
			}
			await _divisionRepository.SaveChangesAsync();

			TempData["SuccessMessage"] = forceReset
				? "Tournament games regenerated and reset from Round 9 standings."
				: "Tournament games generated/updated from Round 9 standings.";
		}
		catch (Exception ex)
		{
			TempData["ErrorMessage"] = $"Error generating tournament games: {ex.Message}";
			_logger.LogError(ex, "Error generating tournament games for division {DivisionId}", divisionId);
		}

		return RedirectToAction(nameof(Entry), new { divisionId, round });
	}

	// POST: Scores/Delete
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Delete(int gameId, int divisionId, int round)
	{
		try
		{
			var deleted = await _scoreService.DeleteScoreAsync(gameId, saveChanges: false);
			if (deleted)
			{
				if (_tournamentService != null)
				{
					await _tournamentService.SyncTournamentProgressionAsync(divisionId, saveChanges: false);
				}

				if (_standingsRefreshService != null)
				{
					await _standingsRefreshService.RefreshDivisionStandingsAsync(divisionId);
				}

				await _scoreRepository.SaveChangesAsync();

				TempData["SuccessMessage"] = "Score deleted successfully.";
				_logger.LogInformation("Score deleted for game {GameId}", gameId);
			}
			else
			{
				TempData["ErrorMessage"] = "Score not found or could not be deleted.";
			}
		}
		catch (Exception ex)
		{
			TempData["ErrorMessage"] = $"Error deleting score: {ex.Message}";
			_logger.LogError(ex, "Error deleting score for game {GameId}", gameId);
		}

		return RedirectToAction(nameof(Entry), new { divisionId, round });
	}

	private static ScoreEntryDto BuildScoreEntryDto(Game game, Score? score, Game? makeupGame)
	{
		var scheduledPacific = TimezoneHelper.ToPacificTime(game.ScheduledDateTime);
		var rescheduledPacific = makeupGame != null ? TimezoneHelper.ToPacificTime(makeupGame.ScheduledDateTime) : (DateTime?)null;

		if (game is EliminationGame elimGame)
		{
			var elimScore = score as EliminationScore;
			var tourneyScore = score as TournamentScore;
			return new EliminationScoreEntryDto
			{
				GameId = game.Id,
				Stage = elimGame.Stage,
				StageLabel = GetStageLabel(elimGame),
				HomeTeamPlaceholder = elimGame.HomeTeamPlaceholder,
				AwayTeamPlaceholder = elimGame.AwayTeamPlaceholder,
				HomeTeamId = game.HomeTeamId,
				HomeTeamName = game.HomeTeam?.Name ?? elimGame.HomeTeamPlaceholder ?? "TBD",
				AwayTeamId = game.AwayTeamId,
				AwayTeamName = game.AwayTeam?.Name ?? elimGame.AwayTeamPlaceholder ?? "TBD",
				ScheduledDateTime = scheduledPacific,
				Location = game.Location,
				Round = game.Round,
				Status = game.Status,
				HomeScore = score?.HomeScore,
				AwayScore = score?.AwayScore,
				IsForfeit = tourneyScore?.IsForfeit ?? false,
				IsDecidedByPenalties = elimScore?.IsDecidedByPenalties ?? false,
				HomePenaltyScore = elimScore?.HomePenaltyScore,
				AwayPenaltyScore = elimScore?.AwayPenaltyScore,
				LastModified = score?.ModifiedAt,
				LastModifiedBy = score?.ModifiedBy,
				RescheduledDateTime = rescheduledPacific,
				RescheduledLocation = makeupGame?.Location,
				RescheduledRound = makeupGame?.Round
			};
		}

		if (game is PoolPlayGame poolGame)
		{
			var poolScore = score as PoolPlayScore;
			var tourneyScore = score as TournamentScore;
			return new PoolPlayScoreEntryDto
			{
				GameId = game.Id,
				Stage = poolGame.Stage,
				StageLabel = GetStageLabel(poolGame),
				HomeTeamPlaceholder = poolGame.HomeTeamPlaceholder,
				AwayTeamPlaceholder = poolGame.AwayTeamPlaceholder,
				HomeTeamId = game.HomeTeamId,
				HomeTeamName = game.HomeTeam?.Name ?? poolGame.HomeTeamPlaceholder ?? "TBD",
				AwayTeamId = game.AwayTeamId,
				AwayTeamName = game.AwayTeam?.Name ?? poolGame.AwayTeamPlaceholder ?? "TBD",
				ScheduledDateTime = scheduledPacific,
				Location = game.Location,
				Round = game.Round,
				Status = game.Status,
				HomeScore = score?.HomeScore,
				AwayScore = score?.AwayScore,
				IsForfeit = tourneyScore?.IsForfeit ?? false,
				HomeRedCards = poolScore?.HomeRedCards ?? 0,
				AwayRedCards = poolScore?.AwayRedCards ?? 0,
				HomeYellowCards = poolScore?.HomeYellowCards ?? 0,
				AwayYellowCards = poolScore?.AwayYellowCards ?? 0,
				LastModified = score?.ModifiedAt,
				LastModifiedBy = score?.ModifiedBy,
				RescheduledDateTime = rescheduledPacific,
				RescheduledLocation = makeupGame?.Location,
				RescheduledRound = makeupGame?.Round
			};
		}

		return new ScoreEntryDto
		{
			GameId = game.Id,
			HomeTeamId = game.HomeTeamId,
			HomeTeamName = game.HomeTeam?.Name ?? "Unknown",
			AwayTeamId = game.AwayTeamId,
			AwayTeamName = game.AwayTeam?.Name ?? "Unknown",
			ScheduledDateTime = scheduledPacific,
			Location = game.Location,
			Round = game.Round,
			Status = game.Status,
			HomeScore = score?.HomeScore,
			AwayScore = score?.AwayScore,
			LastModified = score?.ModifiedAt,
			LastModifiedBy = score?.ModifiedBy,
			RescheduledDateTime = rescheduledPacific,
			RescheduledLocation = makeupGame?.Location,
			RescheduledRound = makeupGame?.Round
		};
	}

	private static string GetStageLabel(TournamentGame game)
	{
		return game.Stage switch
		{
			TournamentStage.Quarterfinal1 => "Quarterfinal 1 (4th vs 5th)",
			TournamentStage.Quarterfinal2 => "Quarterfinal 2 (3rd vs 6th)",
			TournamentStage.Semifinal1 => "Semifinal 1 (1st vs QF1 Winner)",
			TournamentStage.Semifinal2 => "Semifinal 2 (2nd vs QF2 Winner)",
			TournamentStage.Final => "Final",
			_ => game.Stage.GetDisplayLabel()
		};
	}
}
