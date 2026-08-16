using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class DivisionsController : Controller
{
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Season> _seasonRepository;

	public DivisionsController(
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository)
	{
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
	}

	// GET: Divisions
	public async Task<IActionResult> Index()
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

		if (currentSeason == null)
		{
			ViewBag.Message = "No active season found. Please create a season first.";
			return View(new List<Division>());
		}

		ViewBag.SeasonName = currentSeason.Name;

		var divisions = (await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id))
			.OrderBy(d => d.AgeGroup)
			.ThenBy(d => d.Gender)
			.ToList();

		return View(divisions);
	}

	// GET: Divisions/Edit/5
	public async Task<IActionResult> Edit(int id)
	{
		var division = await _divisionRepository.GetByIdAsync(id);
		if (division == null)
		{
			return NotFound();
		}

		return View(division);
	}

	// POST: Divisions/Edit/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int id, Division division)
	{
		if (id != division.Id)
		{
			return BadRequest();
		}

		if (division.ScrimmageRounds < 0 || division.ScrimmageRounds > division.TotalRounds)
		{
			ModelState.AddModelError(nameof(division.ScrimmageRounds), "Scrimmage rounds must be between 0 and the total number of rounds.");
		}

		if (ModelState.IsValid)
		{
			try
			{
				var existing = await _divisionRepository.GetByIdAsync(id);
				if (existing == null)
				{
					return NotFound();
				}

				existing.TotalRounds = division.TotalRounds;
				existing.PlayoffSpots = division.PlayoffSpots;
				existing.ScrimmageRounds = division.ScrimmageRounds;
				existing.CustomMessage = division.CustomMessage;

				_divisionRepository.Update(existing);
				await _divisionRepository.SaveChangesAsync();

				TempData["SuccessMessage"] = "Division updated successfully.";
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				ModelState.AddModelError("", $"Error updating division: {ex.Message}");
			}
		}

		return View(division);
	}
}
