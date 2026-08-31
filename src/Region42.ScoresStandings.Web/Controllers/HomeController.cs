using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Models;

namespace Region42.ScoresStandings.Web.Controllers;

public class HomeController : Controller
{
	private const string DivisionPreferenceCookieName = "PreferredDivisionId";

	private readonly IStandingsService _standingsService;
	private readonly IGameService _gameService;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Season> _seasonRepository;
	private readonly ILogger<HomeController> _logger;

	public HomeController(
		IStandingsService standingsService,
		IGameService gameService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository,
		ILogger<HomeController> logger)
	{
		_standingsService = standingsService;
		_gameService = gameService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
		_logger = logger;
	}

	public IActionResult Index()
	{
		return RedirectToAction(nameof(Standings));
	}

	[AllowAnonymous]
	public async Task<IActionResult> Standings(int? divisionId = null, int? throughRound = null, string? roundSelection = null, int? seasonId = null)
	{
		var seasons = await _seasonRepository.GetAllAsync();
		var selectedSeason = ResolveSelectedSeason(seasons, seasonId);

		if (selectedSeason == null)
		{
			ViewBag.ErrorMessage = "No active season found.";
			return View(new StandingsViewModel());
		}

		var divisionList = await GetDivisionsForSeasonAsync(selectedSeason.Id);

		divisionId = ResolveDivisionId(divisionId, divisionList);

		ViewBag.Seasons = new SelectList(seasons, "Id", "Name", selectedSeason.Id);
		ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", divisionId);

		if (!divisionId.HasValue)
		{
			// No divisions exist
			return View(new StandingsViewModel
			{
				SeasonId = selectedSeason.Id,
				SeasonName = selectedSeason.Name
			});
		}

		var selectedDivision = divisionList.FirstOrDefault(d => d.Id == divisionId.Value);
		if (selectedDivision == null)
		{
			return NotFound();
		}

		int displayRound = throughRound ?? await DetermineDefaultDisplayRoundAsync(divisionId.Value);

		ViewBag.Rounds = BuildRoundSelectList(selectedDivision.TotalRounds, throughRound);

		StandingsResult standings;
		try
		{
			standings = await _standingsService.GetStandingsByRoundAsync(divisionId.Value, displayRound);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error calculating standings for division {DivisionId}", divisionId);
			ViewBag.ErrorMessage = $"Error calculating standings: {ex.Message}";
			return View(new StandingsViewModel
			{
				SeasonId = selectedSeason.Id,
				SeasonName = selectedSeason.Name,
				DivisionName = selectedDivision.Name
			});
		}

		var viewModel = new StandingsViewModel
		{
			SeasonId = selectedSeason.Id,
			SeasonName = selectedSeason.Name,
			DivisionId = divisionId.Value,
			DivisionName = standings.DivisionName,
			ThroughRound = standings.ThroughRound,
			TotalRounds = selectedDivision.TotalRounds,
			CalculatedAt = TimezoneHelper.ToPacificTime(standings.CalculatedAt),
			Standings = standings.Standings,
			ScrimmageRounds = standings.ScrimmageRounds,
			ScrimmageRoundsInRange = standings.ScrimmageRoundsInRange,
			SeasonCustomMessage = selectedSeason.CustomMessage,
			DivisionCustomMessage = selectedDivision.CustomMessage,
			Scores = await GetScoresForDisplayAsync(divisionId.Value, displayRound)
		};

		return View(viewModel);
	}

	/// <summary>
	/// Resolves the season to display: the requested season, falling back to the active
	/// season, then to the first available season.
	/// </summary>
	private static Season? ResolveSelectedSeason(IEnumerable<Season> seasons, int? seasonId)
	{
		var selectedSeason = seasonId.HasValue
			? seasons.FirstOrDefault(s => s.Id == seasonId.Value)
			: seasons.FirstOrDefault(s => s.IsActive);

		return selectedSeason ?? seasons.FirstOrDefault();
	}

	/// <summary>
	/// Retrieves the divisions belonging to the given season, ordered by display name.
	/// </summary>
	private async Task<List<DivisionInfo>> GetDivisionsForSeasonAsync(int seasonId)
	{
		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == seasonId);
		return divisions
			.Select(d => new DivisionInfo(d.Id, $"{d.AgeGroup} {d.Gender}", d.TotalRounds, d.ScrimmageRounds, d.CustomMessage))
			.OrderBy(d => d.Name)
			.ToList();
	}

	/// <summary>
	/// Determines which division should be displayed (priority: URL parameter > Cookie > first division),
	/// saving the preference to a cookie when the user explicitly selected a division.
	/// </summary>
	private int? ResolveDivisionId(int? divisionId, List<DivisionInfo> divisionList)
	{
		// If a division is specified but doesn't belong to the selected season, reset it
		if (divisionId.HasValue && !divisionList.Any(d => d.Id == divisionId.Value))
		{
			divisionId = null;
		}

		if (divisionId.HasValue)
		{
			// User explicitly selected a division - save to cookie
			SaveDivisionPreference(divisionId.Value);
			return divisionId;
		}

		// Try to get from cookie
		if (Request.Cookies.TryGetValue(DivisionPreferenceCookieName, out var cookieValue)
			&& int.TryParse(cookieValue, out int preferredDivisionId)
			&& divisionList.Any(d => d.Id == preferredDivisionId))
		{
			_logger.LogDebug("Using division {DivisionId} from cookie preference", preferredDivisionId);
			return preferredDivisionId;
		}

		// Fall back to first division if cookie not found or invalid
		if (divisionList.Any())
		{
			var defaultDivisionId = divisionList.First().Id;
			_logger.LogDebug("Using first division {DivisionId} as default", defaultDivisionId);
			return defaultDivisionId;
		}

		return null;
	}

	/// <summary>
	/// Determines the default round to display when the caller did not request a specific round:
	/// the most recent completed round, or round 1 if no games are completed (or none exist).
	/// </summary>
	private async Task<int> DetermineDefaultDisplayRoundAsync(int divisionId)
	{
		var allGames = await _gameService.GetGamesByDivisionAsync(divisionId);
		var completedGames = allGames.Where(g => g.Score?.HomeScore.HasValue == true && g.Score?.AwayScore.HasValue == true).ToList();

		return completedGames.Count != 0 ? completedGames.Max(g => g.Round) : 1;
	}

	/// <summary>
	/// Builds the "through round" dropdown (1 to TotalRounds, plus an "All Rounds" option).
	/// </summary>
	private static SelectList BuildRoundSelectList(int totalRounds, int? throughRound)
	{
		var roundOptions = new List<SelectListItem>
		{
			new SelectListItem { Value = "", Text = "All Rounds" }
		};
		roundOptions.AddRange(Enumerable.Range(1, totalRounds)
			.Select(r => new SelectListItem
			{
				Value = r.ToString(),
				Text = $"Through Round {r}"
			}));

		return new SelectList(roundOptions, "Value", "Text", throughRound?.ToString() ?? "");
	}

	/// <summary>
	/// Fetches the games/scores for the given division and round for display.
	/// Returns an empty list (and logs a warning) if the scores could not be retrieved,
	/// since standings are more important than scores.
	/// </summary>
	private async Task<List<GameScoreDisplay>> GetScoresForDisplayAsync(int divisionId, int displayRound)
	{
		try
		{
			var games = await _gameService.GetGamesByDivisionAndRoundAsync(divisionId, displayRound);

			return games
				.OrderBy(g => g.Round)
				.ThenBy(g => g.ScheduledDateTime)
				.Select(g => new GameScoreDisplay
				{
					GameId = g.Id,
					HomeTeamName = g.HomeTeam?.Name ?? "Unknown",
					AwayTeamName = g.AwayTeam?.Name ?? "Unknown",
					HomeScore = g.Score?.HomeScore,
					AwayScore = g.Score?.AwayScore,
					ScheduledDateTime = TimezoneHelper.ToPacificTime(g.ScheduledDateTime),
					Location = g.Location,
					Round = g.Round,
					IsCancelled = g.Status == GameStatus.Cancelled
				})
				.ToList();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error fetching scores for division {DivisionId}", divisionId);
			return new List<GameScoreDisplay>();
		}
	}

	/// <summary>
	/// Lightweight projection of a division used when building the division dropdown and resolving standings.
	/// </summary>
	private sealed record DivisionInfo(int Id, string Name, int TotalRounds, int ScrimmageRounds, string? CustomMessage);

	/// <summary>
	/// Saves the user's division preference to a cookie.
	/// Cookie expires on July 31 or December 31, whichever is later.
	/// July 31 = before new season starts, December 31 = after season ends.
	/// </summary>
	private void SaveDivisionPreference(int divisionId)
	{
		var cookieOptions = new CookieOptions
		{
			Expires = GetSeasonalCookieExpiration(),
			HttpOnly = true,
			Secure = true, // Only send over HTTPS
			SameSite = SameSiteMode.Lax,
			IsEssential = false // Not essential for core functionality
		};

		Response.Cookies.Append(DivisionPreferenceCookieName, divisionId.ToString(), cookieOptions);
		_logger.LogDebug("Saved division preference {DivisionId} to cookie, expires {Expiration}",
			divisionId, cookieOptions.Expires);
	}

	/// <summary>
	/// Calculates cookie expiration date: July 31 or December 31, whichever is later.
	/// </summary>
	private static DateTimeOffset GetSeasonalCookieExpiration()
	{
		var now = DateTime.Now;
		var currentYear = now.Year;

		// July 31 of current year (before new season)
		var july31 = new DateTime(currentYear, 7, 31, 23, 59, 59);

		// December 31 of current year (after season ends)
		var december31 = new DateTime(currentYear, 12, 31, 23, 59, 59);

		// Use whichever is later than now
		if (now < july31)
		{
			return new DateTimeOffset(july31);
		}
		else
		{
			return new DateTimeOffset(december31);
		}
	}

	public IActionResult Privacy()
	{
		return View();
	}

	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
	public IActionResult Error()
	{
		return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
	}
}
