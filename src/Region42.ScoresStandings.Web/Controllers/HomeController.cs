using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
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
    public async Task<IActionResult> Standings(int? divisionId, int? throughRound, string? roundSelection)
    {
        var seasons = await _seasonRepository.GetAllAsync();
        var currentSeason = seasons.FirstOrDefault(s => s.IsActive);

        if (currentSeason == null)
        {
            ViewBag.ErrorMessage = "No active season found.";
            return View(new StandingsViewModel());
        }

        var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == currentSeason.Id);
        var divisionList = divisions.Select(d => new
        {
            Id = d.Id,
            Name = $"{d.AgeGroup} {d.Gender}",
            TotalRounds = d.TotalRounds,
            ScrimmageRounds = d.ScrimmageRounds,
            CustomMessage = d.CustomMessage
        }).OrderBy(d => d.Name).ToList();

        // Determine division to display (priority: URL parameter > Cookie > First division)
        if (!divisionId.HasValue)
        {
            // Try to get from cookie
            if (Request.Cookies.TryGetValue(DivisionPreferenceCookieName, out var cookieValue) 
                && int.TryParse(cookieValue, out int preferredDivisionId))
            {
                // Verify the division still exists in current season
                if (divisionList.Any(d => d.Id == preferredDivisionId))
                {
                    divisionId = preferredDivisionId;
                    _logger.LogDebug("Using division {DivisionId} from cookie preference", divisionId);
                }
            }

            // Fall back to first division if cookie not found or invalid
            if (!divisionId.HasValue && divisionList.Any())
            {
                divisionId = divisionList.First().Id;
                _logger.LogDebug("Using first division {DivisionId} as default", divisionId);
            }
        }
        else
        {
            // User explicitly selected a division - save to cookie
            SaveDivisionPreference(divisionId.Value);
        }

        ViewBag.Divisions = new SelectList(divisionList, "Id", "Name", divisionId);

        if (!divisionId.HasValue)
        {
            // No divisions exist
            return View(new StandingsViewModel
            {
                SeasonName = currentSeason.Name
            });
        }

        var selectedDivision = divisionList.FirstOrDefault(d => d.Id == divisionId.Value);
        if (selectedDivision == null)
        {
            return NotFound();
        }

        // Determine effective round to display
        // If user explicitly selected "All Rounds", use current standings (latest round with scores)
        // Otherwise, determine default round: most recent completed round, or round 1 if none completed
        bool showAllRounds = string.IsNullOrEmpty(roundSelection) && !throughRound.HasValue;
        int displayRound;

        if (!throughRound.HasValue)
        {
            var allGames = await _gameService.GetGamesByDivisionAsync(divisionId.Value);
            var completedGames = allGames.Where(g => g.Score?.HomeScore.HasValue == true && g.Score?.AwayScore.HasValue == true).ToList();

            if (completedGames.Any())
            {
                // Get the most recent completed round
                displayRound = completedGames.Max(g => g.Round);
            }
            else if (allGames.Any())
            {
                // Games exist but none completed - show round 1 with zero points
                displayRound = 1;
            }
            else
            {
                // No games at all - default to round 1
                displayRound = 1;
            }
        }
        else
        {
            displayRound = throughRound.Value;
        }

        // Create round dropdown (1 to TotalRounds, plus "All" option)
        var roundOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All Rounds" }
        };
        roundOptions.AddRange(Enumerable.Range(1, selectedDivision.TotalRounds)
            .Select(r => new SelectListItem
            {
                Value = r.ToString(),
                Text = $"Through Round {r}"
            }));
        ViewBag.Rounds = new SelectList(roundOptions, "Value", "Text", throughRound?.ToString() ?? "");

        // Calculate standings
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
                SeasonName = currentSeason.Name,
                DivisionName = selectedDivision.Name
            });
        }

        var viewModel = new StandingsViewModel
        {
            SeasonName = currentSeason.Name,
            DivisionId = divisionId.Value,
            DivisionName = standings.DivisionName,
            ThroughRound = standings.ThroughRound,
            TotalRounds = selectedDivision.TotalRounds,
            CalculatedAt = standings.CalculatedAt,
            Standings = standings.Standings,
            ScrimmageRounds = standings.ScrimmageRounds,
            ScrimmageRoundsInRange = standings.ScrimmageRoundsInRange,
            SeasonCustomMessage = currentSeason.CustomMessage,
            DivisionCustomMessage = selectedDivision.CustomMessage
        };

        // Fetch scores for display - for the specific display round
        try
        {
            var games = await _gameService.GetGamesByDivisionAndRoundAsync(divisionId.Value, displayRound);

            viewModel.Scores = games
                .OrderBy(g => g.Round)
                .ThenBy(g => g.ScheduledDateTime)
                .Select(g => new GameScoreDisplay
                {
                    GameId = g.Id,
                    HomeTeamName = g.HomeTeam.Name,
                    AwayTeamName = g.AwayTeam.Name,
                    HomeScore = g.Score?.HomeScore,
                    AwayScore = g.Score?.AwayScore,
                    ScheduledDateTime = TimezoneHelper.ToPacificTime(g.ScheduledDateTime),
                    Location = g.Location,
                    Round = g.Round
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching scores for division {DivisionId}", divisionId);
            // Continue without scores - standings are more important
        }

        return View(viewModel);
    }

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
