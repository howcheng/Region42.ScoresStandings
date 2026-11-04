using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.DTOs;
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
	private readonly ITournamentService? _tournamentService;

	public HomeController(
		IStandingsService standingsService,
		IGameService gameService,
		IRepository<Division> divisionRepository,
		IRepository<Season> seasonRepository,
		ILogger<HomeController> logger,
		ITournamentService? tournamentService = null)
	{
		_standingsService = standingsService;
		_gameService = gameService;
		_divisionRepository = divisionRepository;
		_seasonRepository = seasonRepository;
		_logger = logger;
		_tournamentService = tournamentService;
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

		int displayRound = throughRound ?? await DetermineDefaultDisplayRoundAsync(selectedDivision);

		ViewBag.Rounds = BuildRoundSelectList(selectedDivision.TotalRounds, throughRound, selectedDivision.EndOfSeasonTournamentFormat);

		TournamentDisplayDto? tournamentDisplay = null;
		if (selectedDivision.EndOfSeasonTournamentFormat != TournamentFormatType.None &&
			displayRound == selectedDivision.TotalRounds &&
			_tournamentService != null)
		{
			try
			{
				tournamentDisplay = await _tournamentService.GetTournamentBracketAsync(divisionId.Value);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error loading tournament bracket for division {DivisionId}", divisionId);
			}
		}

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
			Tournament = tournamentDisplay,
			IsFinalStandings = standings.IsFinalStandings || (tournamentDisplay?.IsCompleted == true),
			TournamentWinnerTeamId = standings.TournamentWinnerTeamId ?? tournamentDisplay?.ChampionTeamId,
			Scores = await GetScoresForDisplayAsync(divisionId.Value, displayRound)
		};

		return View(viewModel);
	}

	private static Season? ResolveSelectedSeason(IEnumerable<Season> seasons, int? seasonId)
	{
		var selectedSeason = seasonId.HasValue
			? seasons.FirstOrDefault(s => s.Id == seasonId.Value)
			: seasons.FirstOrDefault(s => s.IsActive);

		return selectedSeason ?? seasons.FirstOrDefault();
	}

	private async Task<List<DivisionInfo>> GetDivisionsForSeasonAsync(int seasonId)
	{
		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == seasonId);
		return divisions
			.Select(d => new DivisionInfo(d.Id, $"{d.AgeGroup} {d.Gender}", d.TotalRounds, d.ScrimmageRounds, d.CustomMessage, d.EndOfSeasonTournamentFormat))
			.OrderBy(d => d.Name)
			.ToList();
	}

	private int? ResolveDivisionId(int? divisionId, List<DivisionInfo> divisionList)
	{
		if (divisionId.HasValue && !divisionList.Any(d => d.Id == divisionId.Value))
		{
			divisionId = null;
		}

		if (divisionId.HasValue)
		{
			SaveDivisionPreference(divisionId.Value);
			return divisionId;
		}

		if (Request.Cookies.TryGetValue(DivisionPreferenceCookieName, out var cookieValue)
			&& int.TryParse(cookieValue, out int preferredDivisionId)
			&& divisionList.Any(d => d.Id == preferredDivisionId))
		{
			_logger.LogDebug("Using division {DivisionId} from cookie preference", preferredDivisionId);
			return preferredDivisionId;
		}

		if (divisionList.Any())
		{
			var defaultDivisionId = divisionList.First().Id;
			_logger.LogDebug("Using first division {DivisionId} as default", defaultDivisionId);
			return defaultDivisionId;
		}

		return null;
	}

	private async Task<int> DetermineDefaultDisplayRoundAsync(DivisionInfo division)
	{
		var allGames = (await _gameService.GetGamesByDivisionAsync(division.Id)).ToList();
		var completedGames = allGames
			.Where(g => g.Score?.HomeScore.HasValue == true && g.Score?.AwayScore.HasValue == true)
			.ToList();

		if (division.EndOfSeasonTournamentFormat != TournamentFormatType.None && division.TotalRounds > 1)
		{
			if (allGames.Any(g => g is TournamentGame))
			{
				return division.TotalRounds;
			}

			var lastRegularRound = division.TotalRounds - 1;
			var hasCompletedInLastRegularRound = completedGames.Any(g => g.Round == lastRegularRound);
			var hasScheduledRegularGames = allGames.Any(g => g.Round <= lastRegularRound && g is not TournamentGame && g.Status is GameStatus.Scheduled or GameStatus.Unscheduled);
			if (hasCompletedInLastRegularRound && !hasScheduledRegularGames)
			{
				return division.TotalRounds;
			}
		}

		return completedGames.Count != 0 ? completedGames.Max(g => g.Round) : 1;
	}

	private static SelectList BuildRoundSelectList(int totalRounds, int? throughRound, TournamentFormatType tournamentFormat)
	{
		var hasTournament = tournamentFormat != TournamentFormatType.None;
		var roundOptions = new List<SelectListItem>
		{
			new SelectListItem { Value = "", Text = "All Rounds" }
		};
		roundOptions.AddRange(Enumerable.Range(1, totalRounds)
			.Select(r => new SelectListItem
			{
				Value = r.ToString(),
				Text = hasTournament && r == totalRounds
					? $"Through Round {r} (Tournament)"
					: $"Through Round {r}"
			}));

		return new SelectList(roundOptions, "Value", "Text", throughRound?.ToString() ?? "");
	}

	private async Task<List<GameScoreDisplay>> GetScoresForDisplayAsync(int divisionId, int displayRound)
	{
		try
		{
			var games = (await _gameService.GetGamesByDivisionAndRoundAsync(divisionId, displayRound))
				.OrderBy(g => g.Round)
				.ThenBy(g => g.ScheduledDateTime)
				.ToList();

			var displays = new List<GameScoreDisplay>(games.Count);
			foreach (var g in games)
			{
				Game? makeupGame = null;
				if (g.Status == GameStatus.Rescheduled && g.RescheduledToGameId.HasValue)
				{
					makeupGame = await _gameService.GetGameByIdAsync(g.RescheduledToGameId.Value);
				}

				displays.Add(new GameScoreDisplay
				{
					GameId = g.Id,
					HomeTeamName = g.HomeTeam?.Name ?? "Unknown",
					AwayTeamName = g.AwayTeam?.Name ?? "Unknown",
					HomeScore = g.Score?.HomeScore,
					AwayScore = g.Score?.AwayScore,
					ScheduledDateTime = TimezoneHelper.ToPacificTime(g.ScheduledDateTime),
					Location = g.Location,
					Round = g.Round,
					IsCancelled = g.Status == GameStatus.Cancelled,
					IsRescheduled = g.Status == GameStatus.Rescheduled,
					RescheduledDateTime = makeupGame != null ? TimezoneHelper.ToPacificTime(makeupGame.ScheduledDateTime) : null,
					RescheduledLocation = makeupGame?.Location
				});
			}

			return displays;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Error fetching scores for division {DivisionId}", divisionId);
			return new List<GameScoreDisplay>();
		}
	}

	private sealed record DivisionInfo(
		int Id,
		string Name,
		int TotalRounds,
		int ScrimmageRounds,
		string? CustomMessage,
		TournamentFormatType EndOfSeasonTournamentFormat);

	private void SaveDivisionPreference(int divisionId)
	{
		var cookieOptions = new CookieOptions
		{
			Expires = GetSeasonalCookieExpiration(),
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.Lax,
			IsEssential = false
		};

		Response.Cookies.Append(DivisionPreferenceCookieName, divisionId.ToString(), cookieOptions);
		_logger.LogDebug("Saved division preference {DivisionId} to cookie, expires {Expiration}",
			divisionId, cookieOptions.Expires);
	}

	private static DateTimeOffset GetSeasonalCookieExpiration()
	{
		var now = DateTime.Now;
		var currentYear = now.Year;

		var july31 = new DateTime(currentYear, 7, 31, 23, 59, 59);
		var december31 = new DateTime(currentYear, 12, 31, 23, 59, 59);

		if (now < july31)
		{
			return new DateTimeOffset(july31);
		}

		return new DateTimeOffset(december31);
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
