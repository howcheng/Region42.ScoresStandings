using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

public class StandingsService : IStandingsService
{
	private readonly ICompetitionDataContext _context;
	private readonly ILogger<StandingsService> _logger;

	public StandingsService(
		ICompetitionDataContext context,
		ILogger<StandingsService> logger)
	{
		_context = context;
		_logger = logger;
	}

	public async Task<StandingsResult> GetCurrentStandingsAsync(int divisionId)
	{
		await _context.LoadAllAsync();

		var division = _context.Divisions.FirstOrDefault(d => d.Id == divisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", divisionId);
			throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));
		}

		var games = _context.Games
			.Where(g => g.DivisionId == divisionId && g.Status == GameStatus.Completed)
			.ToList();

		var latestRound = games.Any() ? games.Max(g => g.Round) : division.TotalRounds;
		return await GetStandingsByRoundAsync(divisionId, latestRound);
	}

	public async Task<StandingsResult> GetStandingsByRoundAsync(int divisionId, int throughRound)
	{
		await _context.LoadAllAsync();

		var division = _context.Divisions.FirstOrDefault(d => d.Id == divisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", divisionId);
			throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));
		}

		if (throughRound < 0 || throughRound > division.TotalRounds)
		{
			throw new ArgumentException($"Round must be between 0 and {division.TotalRounds}.", nameof(throughRound));
		}

		var cached = _context.GetStandingsByRound(divisionId);
		if (cached.TryGetValue(throughRound.ToString(), out var snapshot))
		{
			return StandingsDocumentMapper.ToEntity(snapshot, divisionId, GetDivisionName(division));
		}

		return CalculateStandings(division, throughRound);
	}

	public async Task<IEnumerable<StandingsResult>> GetStandingsBySeasonAsync(int seasonId)
	{
		await _context.LoadAllAsync();

		var divisions = _context.Divisions.Where(d => d.SeasonId == seasonId).ToList();
		if (!divisions.Any())
		{
			_logger.LogWarning("No divisions found for season {SeasonId}", seasonId);
			return Enumerable.Empty<StandingsResult>();
		}

		var results = new List<StandingsResult>();
		foreach (var division in divisions)
		{
			try
			{
				results.Add(await GetCurrentStandingsAsync(division.Id));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating standings for division {DivisionId}", division.Id);
			}
		}

		return results;
	}

	public Task<StandingsResult> RecalculateStandingsAsync(int divisionId)
	{
		return GetCurrentStandingsAsync(divisionId);
	}

	private StandingsResult CalculateStandings(Domain.Entities.Division division, int throughRound)
	{
		var teams = _context.Teams.Where(t => t.DivisionId == division.Id).ToList();
		var games = _context.Games.Where(g => g.DivisionId == division.Id).ToList();
		var gameIds = games.Select(g => g.Id).ToHashSet();
		var scores = _context.Scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = teams.Select(t => t.Id).ToHashSet();
		var volunteerPoints = _context.VolunteerPoints.Where(vp => teamIds.Contains(vp.TeamId)).ToList();

		return StandingsCalculator.Calculate(
			division,
			teams,
			games,
			scores,
			volunteerPoints,
			_context.Settings.MinVolunteerPointsForPlayoff,
			throughRound);
	}

	private static string GetDivisionName(Domain.Entities.Division division)
	{
		var ageGroup = division.AgeGroup switch
		{
			AgeGroup.U10 => "10U",
			AgeGroup.U12 => "12U",
			AgeGroup.U14 => "14U",
			_ => division.AgeGroup.ToString()
		};

		var gender = division.Gender == Gender.Boys ? "Boys" : "Girls";
		return $"{ageGroup} {gender}";
	}
}
