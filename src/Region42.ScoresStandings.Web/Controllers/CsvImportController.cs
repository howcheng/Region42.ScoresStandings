using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class CsvImportController : Controller
{
	private readonly ICsvImportService _csvImportService;
	private readonly ISeasonService _seasonService;
	private readonly IRepository<Season> _seasonRepository;
	private readonly ILogger<CsvImportController> _logger;

	public CsvImportController(
		ICsvImportService csvImportService,
		ISeasonService seasonService,
		IRepository<Season> seasonRepository,
		ILogger<CsvImportController> logger)
	{
		_csvImportService = csvImportService;
		_seasonService = seasonService;
		_seasonRepository = seasonRepository;
		_logger = logger;
	}

	// GET: CsvImport/Upload
	public async Task<IActionResult> Upload()
	{
		var emptySeasons = await _seasonService.GetEmptySeasonsAsync();
		var defaultSeasonName = _seasonService.GetDefaultSeasonName();

		// Check if default season exists
		var allSeasons = await _seasonRepository.GetAllAsync();
		var defaultSeason = allSeasons.FirstOrDefault(s => s.Name == defaultSeasonName);

		bool canReplaceDefault = false;
		if (defaultSeason != null)
		{
			canReplaceDefault = await _seasonService.CanReplaceGamesAsync(defaultSeason.Id);
		}

		// Build season selection list
		var seasonOptions = new List<SelectListItem>();

		// Add empty seasons
		foreach (var season in emptySeasons)
		{
			seasonOptions.Add(new SelectListItem
			{
				Value = season.Id.ToString(),
				Text = $"{season.Name} (Empty)"
			});
		}

		// Add default season if it can be replaced
		if (defaultSeason != null && canReplaceDefault && !emptySeasons.Any(s => s.Id == defaultSeason.Id))
		{
			seasonOptions.Add(new SelectListItem
			{
				Value = defaultSeason.Id.ToString(),
				Text = $"{defaultSeason.Name} (Replace Games)"
			});
		}

		// Add "Create New" option
		seasonOptions.Add(new SelectListItem
		{
			Value = "0",
			Text = "Create New Season..."
		});

		ViewBag.SeasonOptions = new SelectList(seasonOptions, "Value", "Text");
		ViewBag.DefaultSeasonName = defaultSeasonName;
		ViewBag.HasEmptySeasons = emptySeasons.Any();
		ViewBag.CanReplaceDefault = canReplaceDefault;

		return View();
	}

	// POST: CsvImport/Upload
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Upload(IFormFile? csvFile, int seasonId, string? newSeasonName = null)
	{
		if (csvFile == null || csvFile.Length == 0)
		{
			ModelState.AddModelError("csvFile", "Please select a CSV file to upload.");
			return await Upload(); // Reload the view with season options
		}

		if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
		{
			ModelState.AddModelError("csvFile", "Only CSV files are allowed.");
			return await Upload();
		}

		// Handle season selection/creation
		Season targetSeason;
		bool replaceGames = false;

		if (seasonId == 0)
		{
			// Create new season
			var seasonName = string.IsNullOrWhiteSpace(newSeasonName) 
				? _seasonService.GetDefaultSeasonName() 
				: newSeasonName;

			targetSeason = await _seasonService.CreateSeasonAsync(seasonName, setAsActive: true);
			_logger.LogInformation("Created new season: {SeasonName} (ID: {SeasonId})", targetSeason.Name, targetSeason.Id);
		}
		else
		{
			// Use existing season
			targetSeason = await _seasonRepository.GetByIdAsync(seasonId);

			if (targetSeason == null)
			{
				ModelState.AddModelError("", "Selected season not found.");
				return await Upload();
			}

			// Check if we need to replace games
			var hasGames = !(await _seasonService.GetEmptySeasonsAsync()).Any(s => s.Id == seasonId);

			if (hasGames)
			{
				// Verify replacement is allowed
				if (!await _seasonService.CanReplaceGamesAsync(seasonId))
				{
					ModelState.AddModelError("", 
						"Cannot replace games for this season. Round 1 already has scores entered.");
					return await Upload();
				}

				replaceGames = true;
				_logger.LogWarning("Will replace games for season: {SeasonName} (ID: {SeasonId})", 
					targetSeason.Name, targetSeason.Id);
			}
		}

		try
		{
			using var stream = csvFile.OpenReadStream();

			// First validate the CSV
			var validationResult = await _csvImportService.ValidateCsvAsync(stream, targetSeason.Id);

			if (!validationResult.IsValid)
			{
				ViewBag.ValidationErrors = validationResult.Errors;
				ViewBag.ValidationWarnings = validationResult.Warnings;
				ViewBag.TotalRows = validationResult.TotalRows;
				ViewBag.ValidRows = validationResult.ValidRows;
				ViewBag.SkippedRows = validationResult.SkippedRows;
				_logger.LogWarning("CSV validation failed with {ErrorCount} errors", validationResult.Errors.Count);
				return await Upload();
			}

			// If we need to replace games, do it before importing
			if (replaceGames)
			{
				await _seasonService.DeleteAllGamesForSeasonAsync(targetSeason.Id);
				_logger.LogInformation("Deleted all games for season {SeasonId} before import", targetSeason.Id);
			}

			// If validation passes, get preview
			stream.Position = 0; // Reset stream
			var preview = await _csvImportService.PreviewImportAsync(stream, targetSeason.Id);

			_logger.LogInformation("Preview generated: {TeamCount} teams, {GameCount} games", 
				preview.Teams.Count, preview.Games.Count);

			// Store preview data for confirmation
			var teamsJson = System.Text.Json.JsonSerializer.Serialize(preview.Teams);
			var gamesJson = System.Text.Json.JsonSerializer.Serialize(preview.Games);

			_logger.LogInformation("Serialized preview data - Teams: {TeamsLength} chars, Games: {GamesLength} chars",
				teamsJson.Length, gamesJson.Length);

			TempData["CsvPreviewTeams"] = teamsJson;
			TempData["CsvPreviewGames"] = gamesJson;
			TempData["SeasonId"] = targetSeason.Id;
			TempData["SeasonName"] = targetSeason.Name;
			TempData["FileName"] = csvFile.FileName;
			TempData["ReplaceGames"] = replaceGames;

			return RedirectToAction(nameof(Preview));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing CSV file");
			ModelState.AddModelError("", $"Error processing file: {ex.Message}");
			return await Upload();
		}
	}

	// GET: CsvImport/Preview
	public IActionResult Preview()
	{
		_logger.LogInformation("Preview action called - checking TempData...");
		_logger.LogInformation("TempData keys: {Keys}", string.Join(", ", TempData.Keys));

		if (TempData["CsvPreviewTeams"] == null || TempData["CsvPreviewGames"] == null)
		{
			_logger.LogWarning("Preview TempData missing - redirecting to Upload");
			return RedirectToAction(nameof(Upload));
		}

		var teamsJson = TempData["CsvPreviewTeams"]?.ToString();
		var gamesJson = TempData["CsvPreviewGames"]?.ToString();

		_logger.LogInformation("Teams JSON length: {Length}", teamsJson?.Length ?? 0);
		_logger.LogInformation("Games JSON length: {Length}", gamesJson?.Length ?? 0);

		if (string.IsNullOrEmpty(teamsJson) || string.IsNullOrEmpty(gamesJson))
		{
			_logger.LogWarning("Preview JSON is empty - redirecting to Upload");
			return RedirectToAction(nameof(Upload));
		}

		var teams = System.Text.Json.JsonSerializer.Deserialize<List<CsvTeamPreview>>(teamsJson);
		var games = System.Text.Json.JsonSerializer.Deserialize<List<CsvGamePreview>>(gamesJson);

		_logger.LogInformation("Deserialized {TeamCount} teams and {GameCount} games", 
			teams?.Count ?? 0, games?.Count ?? 0);

		ViewBag.Teams = teams;
		ViewBag.Games = games;
		ViewBag.SeasonId = TempData["SeasonId"];
		ViewBag.FileName = TempData["FileName"];

		// Keep in TempData for the import action
		TempData.Keep("CsvPreviewTeams");
		TempData.Keep("CsvPreviewGames");
		TempData.Keep("SeasonId");

		return View();
	}

	// POST: CsvImport/Import
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Import(IFormFile csvFile, int seasonId)
	{
		if (csvFile == null || csvFile.Length == 0)
		{
			TempData["ErrorMessage"] = "CSV file is required.";
			return RedirectToAction(nameof(Upload));
		}

		try
		{
			using var stream = csvFile.OpenReadStream();
			var result = await _csvImportService.ImportCsvAsync(stream, seasonId);

			if (result.Success)
			{
				_logger.LogInformation("CSV import successful: {TeamsCreated} teams created, {TeamsUpdated} teams updated, {GamesCreated} games created",
					result.TeamsCreated, result.TeamsUpdated, result.GamesCreated);

				TempData["SuccessMessage"] = $"Import completed successfully! " +
					$"Teams created: {result.TeamsCreated}, Teams updated: {result.TeamsUpdated}, " +
					$"Games created: {result.GamesCreated}, Rows skipped: {result.RowsSkipped}";
			}
			else
			{
				_logger.LogWarning("CSV import failed: {Message}", result.Message);
				TempData["ErrorMessage"] = $"Import failed: {result.Message}";

				if (result.Errors.Any())
				{
					TempData["ImportErrors"] = string.Join("; ", result.Errors);
				}
			}

			return RedirectToAction(nameof(Upload));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error importing CSV file");
			TempData["ErrorMessage"] = $"Error importing file: {ex.Message}";
			return RedirectToAction(nameof(Upload));
		}
	}
}
