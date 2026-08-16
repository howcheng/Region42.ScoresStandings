using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Controllers;

[Authorize(Policy = "AdminPolicy")]
public class SeasonsController : Controller
{
	private readonly IRepository<Season> _seasonRepository;

	public SeasonsController(IRepository<Season> seasonRepository)
	{
		_seasonRepository = seasonRepository;
	}

	// GET: Seasons
	public async Task<IActionResult> Index()
	{
		var seasons = (await _seasonRepository.GetAllAsync())
			.OrderByDescending(s => s.Year)
			.ThenBy(s => s.Name)
			.ToList();

		return View(seasons);
	}

	// GET: Seasons/Edit/5
	public async Task<IActionResult> Edit(int id)
	{
		var season = await _seasonRepository.GetByIdAsync(id);
		if (season == null)
		{
			return NotFound();
		}

		return View(season);
	}

	// POST: Seasons/Edit/5
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(int id, Season season)
	{
		if (id != season.Id)
		{
			return BadRequest();
		}

		if (ModelState.IsValid)
		{
			try
			{
				var existing = await _seasonRepository.GetByIdAsync(id);
				if (existing == null)
				{
					return NotFound();
				}

				existing.Name = season.Name;
				existing.CustomMessage = season.CustomMessage;

				_seasonRepository.Update(existing);
				await _seasonRepository.SaveChangesAsync();

				TempData["SuccessMessage"] = "Season updated successfully.";
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				ModelState.AddModelError("", $"Error updating season: {ex.Message}");
			}
		}

		return View(season);
	}
}
