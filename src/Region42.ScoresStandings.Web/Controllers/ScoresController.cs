using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
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

	public ScoresController(
		IScoreService scoreService,
		IGameService gameService,
		ITeamService teamService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository,
		IRepository<Score> scoreRepository,
		ILogger<ScoresController> logger)
	{
		_scoreService = scoreService;
		_gameService = gameService;
		_teamService = teamService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
		_scoreRepository = scoreRepository;
		_logger = logger;
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

		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id);
		var divisionList = divisions.Select(d => new
		{
			Id = d.Id,
			Name = $"{d.AgeGroup} {d.Gender}",
			TotalRounds = d.TotalRounds
		}).ToList();

		ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", divisionId);

		if (!divisionId.HasValue)
		{
			// No division selected, show empty grid
			ViewBag.Rounds = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
			return View(new List<ScoreEntryDto>());
		}

		var selectedDivision = divisionList.FirstOrDefault(d => d.Id == divisionId.Value);
		if (selectedDivision == null)
		{
			return NotFound();
		}

		// Create round dropdown (1 to TotalRounds)
		var rounds = Enumerable.Range(1, selectedDivision.TotalRounds)
			.Select(r => new SelectListItem
			{
				Value = r.ToString(),
				Text = $"Round {r}"
			});
		ViewBag.Rounds = new SelectList(rounds, "Value", "Text", round);
		ViewBag.SelectedDivisionId = divisionId;

		if (!round.HasValue)
		{
			// No round selected, show empty grid
			return View(new List<ScoreEntryDto>());
		}

		// Get games for selected division and round
		var games = await _gameService.GetGamesByDivisionAndRoundAsync(divisionId.Value, round.Value);

		// Get all teams for this division for the dropdowns
		var teams = await _teamService.GetTeamsByDivisionAsync(divisionId.Value);
		var teamList = teams.Select(t => new SelectListItem
		{
			Value = t.Id.ToString(),
			Text = t.Name
		}).ToList();
		ViewBag.Teams = teamList;

		// Build score entry DTOs
		var scoreEntries = new List<ScoreEntryDto>();
		foreach (var game in games.OrderBy(g => g.ScheduledDateTime))
		{
			var score = await _scoreService.GetScoreByGameIdAsync(game.Id);

			scoreEntries.Add(new ScoreEntryDto
			{
				GameId = game.Id,
				HomeTeamId = game.HomeTeamId,
				HomeTeamName = game.HomeTeam?.Name ?? "Unknown",
				AwayTeamId = game.AwayTeamId,
				AwayTeamName = game.AwayTeam?.Name ?? "Unknown",
				ScheduledDateTime = TimezoneHelper.ToPacificTime(game.ScheduledDateTime),
				Location = game.Location,
				Round = game.Round,
				HomeScore = score?.HomeScore,
				AwayScore = score?.AwayScore,
				LastModified = score?.ModifiedAt,
				LastModifiedBy = score?.ModifiedBy
					});
				}

				ViewBag.SelectedRound = round;

				return View(scoreEntries);
			}

	// POST: Scores/Entry
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Entry(List<ScoreUpdateDto> scores, int divisionId, int round)
	{
		if (scores == null || !scores.Any())
		{
			TempData["ErrorMessage"] = "No scores to save.";
			return RedirectToAction(nameof(Entry), new { divisionId, round });
		}

		// Validate team uniqueness: no team should appear more than once in the round
		var teamAppearances = new Dictionary<int, int>();
		foreach (var score in scores)
		{
			// Count home team appearances
			if (teamAppearances.ContainsKey(score.HomeTeamId))
				teamAppearances[score.HomeTeamId]++;
			else
				teamAppearances[score.HomeTeamId] = 1;

			// Count away team appearances
			if (teamAppearances.ContainsKey(score.AwayTeamId))
				teamAppearances[score.AwayTeamId]++;
			else
				teamAppearances[score.AwayTeamId] = 1;

			// Validate home and away teams are different
			if (score.HomeTeamId == score.AwayTeamId)
			{
				TempData["ErrorMessage"] = "A team cannot play against itself. Please check the schedule.";
				return RedirectToAction(nameof(Entry), new { divisionId, round });
			}
		}

		// Check for duplicate team assignments
		var duplicateTeams = teamAppearances.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key).ToList();
		if (duplicateTeams.Any())
		{
			var teams = await _teamService.GetTeamsByDivisionAsync(divisionId);
			var teamNames = teams.Where(t => duplicateTeams.Contains(t.Id))
				.Select(t => t.Name)
				.ToList();
			TempData["ErrorMessage"] = $"The following team(s) appear more than once in this round: {string.Join(", ", teamNames)}. Each team can only have one game per round.";
			return RedirectToAction(nameof(Entry), new { divisionId, round });
		}

		var successCount = 0;
		var errorCount = 0;
		var errors = new List<string>();

		foreach (var scoreUpdate in scores)
		{
			try
			{
				// First, update the game teams if they changed
				var game = await _gameService.GetGameByIdAsync(scoreUpdate.GameId);
				if (game == null)
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId} not found.");
					continue;
				}

				// Validate partial score entry - both scores must be entered or both must be empty
				var hasHomeScore = scoreUpdate.HomeScore.HasValue;
				var hasAwayScore = scoreUpdate.AwayScore.HasValue;
				if (hasHomeScore != hasAwayScore)
				{
					errorCount++;
					errors.Add($"Game {scoreUpdate.GameId}: Both home and away scores must be entered. A game is not complete until both scores are added.");
					_logger.LogWarning("Partial score entry attempted for game {GameId}", scoreUpdate.GameId);
					continue;
				}

				bool gameChanged = false;
				if (game.HomeTeamId != scoreUpdate.HomeTeamId || game.AwayTeamId != scoreUpdate.AwayTeamId)
				{
					game.HomeTeamId = scoreUpdate.HomeTeamId;
					game.AwayTeamId = scoreUpdate.AwayTeamId;
					await _gameService.UpdateGameAsync(game);
					gameChanged = true;
					_logger.LogInformation("Updated teams for game {GameId}: Home={HomeTeamId}, Away={AwayTeamId}",
						scoreUpdate.GameId, scoreUpdate.HomeTeamId, scoreUpdate.AwayTeamId);
				}

				// Update the score only if both scores are provided (game is complete)
				if (hasHomeScore && hasAwayScore)
				{
					await _scoreService.EnterOrUpdateScoreAsync(
						scoreUpdate.GameId,
						scoreUpdate.HomeScore!.Value,
						scoreUpdate.AwayScore!.Value);

					_logger.LogInformation("Score saved for game {GameId}: {HomeScore}-{AwayScore}",
						scoreUpdate.GameId, scoreUpdate.HomeScore, scoreUpdate.AwayScore);
				}

				if (gameChanged || (hasHomeScore && hasAwayScore))
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

		if (successCount > 0)
		{
			TempData["SuccessMessage"] = $"{successCount} game(s) saved successfully.";
		}

		if (errorCount > 0)
		{
			TempData["ErrorMessage"] = $"{errorCount} game(s) failed to save. Errors: {string.Join("; ", errors)}";
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
			var deleted = await _scoreService.DeleteScoreAsync(gameId);
			if (deleted)
			{
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
}
