using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class TeamsController : Controller
{
	private readonly ITeamService _teamService;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Season> _seasonRepository;

	public TeamsController(
		ITeamService teamService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository)
	{
		_teamService = teamService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
	}

	// GET: Teams
	public async Task<IActionResult> Index(int? divisionId)
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

		if (currentSeason == null)
		{
			ViewBag.Message = "No active season found. Please create a season first.";
			return View(new List<Team>());
		}

		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id);
		var divisionList = divisions.Select(d => new
		{
			Id = d.Id,
			Name = $"{d.AgeGroup} {d.Gender}"
		}).ToList();
		ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", divisionId);

		IEnumerable<Team> teams;
		if (divisionId.HasValue)
		{
			teams = await _teamService.GetTeamsByDivisionAsync(divisionId.Value);
		}
		else
		{
			teams = await _teamService.GetTeamsBySeasonAsync(currentSeason.Id);
		}

		return View(teams);
	}

	// GET: Teams/Create
	public async Task<IActionResult> Create()
	{
		await LoadDivisionsAsync();
		return View();
	}

	// POST: Teams/Create
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(Team team)
	{
		if (ModelState.IsValid)
		{
			// Validate team name uniqueness
			if (!await _teamService.IsTeamNameUniqueAsync(team.Name, team.DivisionId))
			{
				ModelState.AddModelError("Name", "A team with this name already exists in the selected division.");
				await LoadDivisionsAsync(team.DivisionId);
				return View(team);
			}

			try
			{
				await _teamService.CreateTeamAsync(team);
				TempData["SuccessMessage"] = $"Team '{team.Name}' created successfully.";
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				ModelState.AddModelError("", $"Error creating team: {ex.Message}");
			}
		}

		await LoadDivisionsAsync(team.DivisionId);
		return View(team);
	}

	// GET: Teams/Edit/5
	public async Task<IActionResult> Edit(int id)
	{
		var team = await _teamService.GetTeamByIdAsync(id);
		if (team == null)
		{
			return NotFound();
		}

		await LoadDivisionsAsync(team.DivisionId);
		return View(team);
	}

	// POST: Teams/Edit/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int id, Team team)
	{
		if (id != team.Id)
		{
			return BadRequest();
		}

		if (ModelState.IsValid)
		{
			// Validate team name uniqueness (excluding current team)
			if (!await _teamService.IsTeamNameUniqueAsync(team.Name, team.DivisionId, team.Id))
			{
				ModelState.AddModelError("Name", "A team with this name already exists in the selected division.");
				await LoadDivisionsAsync(team.DivisionId);
				return View(team);
			}

			try
			{
				await _teamService.UpdateTeamAsync(team);
				TempData["SuccessMessage"] = $"Team '{team.Name}' updated successfully.";
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				ModelState.AddModelError("", $"Error updating team: {ex.Message}");
			}
		}

		await LoadDivisionsAsync(team.DivisionId);
		return View(team);
	}

	// GET: Teams/Delete/5
	public async Task<IActionResult> Delete(int id)
	{
		var team = await _teamService.GetTeamByIdAsync(id);
		if (team == null)
		{
			return NotFound();
		}

		return View(team);
	}

	// POST: Teams/Delete/5
	[HttpPost, ActionName("Delete")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> DeleteConfirmed(int id)
	{
		try
		{
			var team = await _teamService.GetTeamByIdAsync(id);
			if (team == null)
			{
				return NotFound();
			}

			await _teamService.DeactivateTeamAsync(id);
			TempData["SuccessMessage"] = $"Team '{team.Name}' deactivated successfully.";
			return RedirectToAction(nameof(Index));
		}
		catch (InvalidOperationException ex)
		{
			TempData["ErrorMessage"] = ex.Message;
			return RedirectToAction(nameof(Delete), new { id });
		}
		catch (Exception ex)
		{
			TempData["ErrorMessage"] = $"Error deactivating team: {ex.Message}";
			return RedirectToAction(nameof(Delete), new { id });
		}
	}

	private async Task LoadDivisionsAsync(int? selectedDivisionId = null)
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

		if (currentSeason != null)
		{
			var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id);
			var divisionList = divisions.Select(d => new
			{
				Id = d.Id,
				Name = $"{d.AgeGroup} {d.Gender}"
			}).ToList();
			ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", selectedDivisionId);
		}
		else
		{
			ViewBag.Divisions = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
		}
	}
}
