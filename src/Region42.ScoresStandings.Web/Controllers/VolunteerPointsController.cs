using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class VolunteerPointsController : Controller
{
	private readonly IVolunteerPointsService _volunteerPointsService;
	private readonly ITeamService _teamService;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Season> _seasonRepository;
	private readonly ILogger<VolunteerPointsController> _logger;

	public VolunteerPointsController(
		IVolunteerPointsService volunteerPointsService,
		ITeamService teamService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository,
		ILogger<VolunteerPointsController> logger)
	{
		_volunteerPointsService = volunteerPointsService;
		_teamService = teamService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
		_logger = logger;
	}

	// GET: VolunteerPoints/Entry
	public async Task<IActionResult> Entry(int? divisionId)
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

		if (currentSeason == null)
		{
			ViewBag.ErrorMessage = "No active season found.";
			return View();
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
			return View(new VolunteerPointsGridViewModel());
		}

		var selectedDivision = divisionList.FirstOrDefault(d => d.Id == divisionId.Value);
		if (selectedDivision == null)
		{
			return NotFound();
		}

		// Get all teams for the division
		var teams = await _teamService.GetTeamsByDivisionAsync(divisionId.Value);
		var activeTeams = teams.Where(t => t.IsActive).OrderBy(t => t.Name).ToList();

		// Get all volunteer points for the division
		var allPoints = await _volunteerPointsService.GetVolunteerPointsByDivisionAsync(divisionId.Value);
		var pointsLookup = allPoints.ToDictionary(vp => $"{vp.TeamId}-{vp.Round}", vp => vp);

		// Build grid data: rows = teams, columns = rounds
		var gridModel = new VolunteerPointsGridViewModel
		{
			DivisionId = divisionId.Value,
			DivisionName = selectedDivision.Name,
			TotalRounds = selectedDivision.TotalRounds,
			Teams = activeTeams.Select(team => new TeamVolunteerPointsRow
			{
				TeamId = team.Id,
				TeamName = team.Name,
				RoundPoints = Enumerable.Range(1, selectedDivision.TotalRounds)
					.Select(round =>
					{
						var key = $"{team.Id}-{round}";
						var existingPoints = pointsLookup.TryGetValue(key, out var vp) ? vp : null;
						return new RoundPointsCell
						{
							Round = round,
							Points = existingPoints?.Points ?? 0,
							Notes = existingPoints?.Notes ?? string.Empty,
							VolunteerPointsId = existingPoints?.Id
						};
					}).ToList()
			}).ToList()
		};

		return View(gridModel);
	}

	// POST: VolunteerPoints/Entry
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Entry(VolunteerPointsGridViewModel model)
	{
		if (model == null || model.Teams == null || !model.Teams.Any())
		{
			TempData["ErrorMessage"] = "No data to save.";
			return RedirectToAction(nameof(Entry), new { divisionId = model?.DivisionId });
		}

		var successCount = 0;
		var errorCount = 0;
		var errors = new List<string>();

		foreach (var teamRow in model.Teams)
		{
			foreach (var cell in teamRow.RoundPoints)
			{
				try
				{
					// Always save the value - empty textboxes count as zero
					// This allows users to correct previously entered points back to zero
					await _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(
						teamRow.TeamId,
						cell.Round,
						cell.Points,
						cell.Notes ?? string.Empty);

					successCount++;
					_logger.LogInformation("Volunteer points saved for team {TeamId} round {Round}: {Points} points",
						teamRow.TeamId, cell.Round, cell.Points);
				}
				catch (Exception ex)
				{
					errorCount++;
					errors.Add($"Team {teamRow.TeamName} Round {cell.Round}: {ex.Message}");
					_logger.LogError(ex, "Error saving volunteer points for team {TeamId} round {Round}",
						teamRow.TeamId, cell.Round);
				}
			}
		}

		if (successCount > 0)
		{
			TempData["SuccessMessage"] = $"{successCount} volunteer points entry/entries saved successfully.";
		}

		if (errorCount > 0)
		{
			TempData["ErrorMessage"] = $"{errorCount} entry/entries failed to save. Errors: {string.Join("; ", errors.Take(5))}";
			if (errors.Count > 5)
			{
				TempData["ErrorMessage"] += $" ... and {errors.Count - 5} more.";
			}
		}

		return RedirectToAction(nameof(Entry), new { divisionId = model.DivisionId });
	}
}

// View Models for volunteer points grid
public class VolunteerPointsGridViewModel
{
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int TotalRounds { get; set; }
	public List<TeamVolunteerPointsRow> Teams { get; set; } = new();
}

public class TeamVolunteerPointsRow
{
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public List<RoundPointsCell> RoundPoints { get; set; } = new();
}

public class RoundPointsCell
{
	public int Round { get; set; }
	public int Points { get; set; }
	public string Notes { get; set; } = string.Empty;
	public int? VolunteerPointsId { get; set; }  // For tracking existing entries
}
