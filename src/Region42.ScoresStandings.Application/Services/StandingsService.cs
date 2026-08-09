using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for calculating standings with support for point-in-time queries.
/// Implements standard soccer scoring (Win=3pts, Draw=1pt, Loss=0pts) plus volunteer points.
/// Handles divisions with odd number of teams (adjusted for games played).
/// </summary>
public class StandingsService : IStandingsService
{
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Team> _teamRepository;
	private readonly IRepository<Game> _gameRepository;
	private readonly IRepository<Score> _scoreRepository;
	private readonly IRepository<VolunteerPoints> _volunteerPointsRepository;
	private readonly IRepository<Settings> _settingsRepository;
	private readonly ILogger<StandingsService> _logger;

	public StandingsService(
		IRepository<Division> divisionRepository,
		IRepository<Team> teamRepository,
		IRepository<Game> gameRepository,
		IRepository<Score> scoreRepository,
		IRepository<VolunteerPoints> volunteerPointsRepository,
		IRepository<Settings> settingsRepository,
		ILogger<StandingsService> logger)
	{
		_divisionRepository = divisionRepository;
		_teamRepository = teamRepository;
		_gameRepository = gameRepository;
		_scoreRepository = scoreRepository;
		_volunteerPointsRepository = volunteerPointsRepository;
		_settingsRepository = settingsRepository;
		_logger = logger;
	}

	public async Task<StandingsResult> GetCurrentStandingsAsync(int divisionId)
	{
		var division = await _divisionRepository.GetByIdAsync(divisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", divisionId);
			throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));
		}

		// Get the latest completed round
		var games = (await _gameRepository.FindAsync(g => g.DivisionId == divisionId && g.Status == GameStatus.Completed))
			.ToList();

		// If no games completed yet, use total rounds to include all volunteer points
		var latestRound = games.Any() ? games.Max(g => g.Round) : division.TotalRounds;

		return await CalculateStandingsAsync(division, latestRound);
	}

	public async Task<StandingsResult> GetStandingsByRoundAsync(int divisionId, int throughRound)
	{
		var division = await _divisionRepository.GetByIdAsync(divisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", divisionId);
			throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));
		}

		if (throughRound < 0 || throughRound > division.TotalRounds)
		{
			throw new ArgumentException($"Round must be between 0 and {division.TotalRounds}.", nameof(throughRound));
		}

		return await CalculateStandingsAsync(division, throughRound);
	}

	public async Task<IEnumerable<StandingsResult>> GetStandingsBySeasonAsync(int seasonId)
	{
		var divisions = (await _divisionRepository.FindAsync(d => d.SeasonId == seasonId)).ToList();

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
				var standings = await GetCurrentStandingsAsync(division.Id);
				results.Add(standings);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error calculating standings for division {DivisionId}", division.Id);
			}
		}

		return results;
	}

	public async Task<StandingsResult> RecalculateStandingsAsync(int divisionId)
	{
		// Same as GetCurrentStandingsAsync - recalculates from scratch
		return await GetCurrentStandingsAsync(divisionId);
	}

	#region Private Helper Methods

	private async Task<StandingsResult> CalculateStandingsAsync(Division division, int throughRound)
	{
		// Only include Region 42 teams in standings (exclude away region teams)
		var teams = (await _teamRepository.FindAsync(t => 
			t.DivisionId == division.Id && 
			t.IsActive && 
			t.IsRegion42Team))
			.ToList();

		if (!teams.Any())
		{
			_logger.LogWarning("No active teams found for division {DivisionId}", division.Id);
			return new StandingsResult
			{
				DivisionId = division.Id,
				DivisionName = GetDivisionName(division),
				ThroughRound = throughRound,
				CalculatedAt = DateTime.UtcNow,
				Standings = new List<TeamStanding>()
			};
		}

		// Get games through the specified round
		var games = (await _gameRepository.FindAsync(g => 
			g.DivisionId == division.Id && 
			g.Round <= throughRound && 
			g.Status == GameStatus.Completed))
			.ToList();

		// Get scores for those games
		var gameIds = games.Select(g => g.Id).ToHashSet();
		var scores = (await _scoreRepository.GetAllAsync())
			.Where(s => gameIds.Contains(s.GameId))
			.ToList();

		// Get volunteer points through the specified round
		var teamIds = teams.Select(t => t.Id).ToHashSet();
		var volunteerPoints = (await _volunteerPointsRepository.GetAllAsync())
			.Where(vp => teamIds.Contains(vp.TeamId) && vp.Round <= throughRound)
			.ToList();

		// Get league-wide settings for playoff qualification
		var settings = (await _settingsRepository.GetAllAsync()).FirstOrDefault();
		var minVolunteerPoints = settings?.MinVolunteerPointsForPlayoff ?? 0;

		// Calculate standings for each team
		var standings = teams.Select(team => CalculateTeamStanding(team, games, scores, volunteerPoints)).ToList();

		// Sort by total points (desc), goal differential (desc), goals for (desc)
		standings = standings
			.OrderByDescending(s => s.TotalPoints)
			.ThenByDescending(s => s.GoalDifferential)
			.ThenByDescending(s => s.GoalsFor)
			.ThenBy(s => s.TeamName)
			.ToList();

		// Assign ranks
		for (int i = 0; i < standings.Count; i++)
		{
			standings[i].Rank = i + 1;
		}

		// Check if we need to calculate points per game (odd number of teams)
		var gamesPlayedCounts = standings.Select(s => s.GamesPlayed).Distinct().ToList();
		if (gamesPlayedCounts.Count > 1)
		{
			// Teams have played different numbers of games - calculate PPG
			foreach (var standing in standings)
			{
				standing.PointsPerGame = standing.GamesPlayed > 0 
					? Math.Round((decimal)standing.TotalPoints / standing.GamesPlayed, 2)
					: 0;
			}

			_logger.LogInformation("Division {DivisionId} has teams with different games played counts - PPG calculated", 
				division.Id);
		}

		// Determine playoff qualification
		ApplyPlayoffQualification(standings, division.PlayoffSpots, minVolunteerPoints);

		return new StandingsResult
		{
			DivisionId = division.Id,
			DivisionName = GetDivisionName(division),
			ThroughRound = throughRound,
			CalculatedAt = DateTime.UtcNow,
			Standings = standings
		};
	}

	private TeamStanding CalculateTeamStanding(
		Team team,
		List<Game> games,
		List<Score> scores,
		List<VolunteerPoints> volunteerPoints)
	{
		var standing = new TeamStanding
		{
			TeamId = team.Id,
			TeamName = team.Name,
			TeamShortName = team.ShortName
		};

		// Get games where this team played
		var teamGames = games.Where(g => g.HomeTeamId == team.Id || g.AwayTeamId == team.Id).ToList();
		standing.GamesPlayed = teamGames.Count;

		// Calculate stats from each game
		foreach (var game in teamGames)
		{
			var score = scores.FirstOrDefault(s => s.GameId == game.Id);
			if (score == null || !score.HomeScore.HasValue || !score.AwayScore.HasValue)
			{
				continue; // Skip games without scores
			}

			bool isHomeTeam = game.HomeTeamId == team.Id;
			int goalsFor = isHomeTeam ? score.HomeScore.Value : score.AwayScore.Value;
			int goalsAgainst = isHomeTeam ? score.AwayScore.Value : score.HomeScore.Value;

			standing.GoalsFor += goalsFor;
			standing.GoalsAgainst += goalsAgainst;

			// Determine result
			if (goalsFor > goalsAgainst)
			{
				standing.Wins++;
				standing.GamePoints += 3; // Win = 3 points
			}
			else if (goalsFor == goalsAgainst)
			{
				standing.Draws++;
				standing.GamePoints += 1; // Draw = 1 point
			}
			else
			{
				standing.Losses++;
				// Loss = 0 points
			}
		}

		standing.GoalDifferential = standing.GoalsFor - standing.GoalsAgainst;

		// Add volunteer points
		standing.VolunteerPoints = volunteerPoints
			.Where(vp => vp.TeamId == team.Id)
			.Sum(vp => vp.Points);

		// Calculate total points
		standing.TotalPoints = standing.GamePoints + standing.VolunteerPoints;

		return standing;
	}

	private string GetDivisionName(Division division)
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

	/// <summary>
	/// Determines playoff qualification for teams based on rank, volunteer points threshold,
	/// and division playoff spots.
	/// </summary>
	private void ApplyPlayoffQualification(
		List<TeamStanding> standings, 
		int playoffSpots, 
		int minVolunteerPoints)
	{
		int qualifiedCount = 0;

		foreach (var standing in standings)
		{
			// Check if team has enough volunteer points
			var hasMinVolunteerPoints = standing.VolunteerPoints >= minVolunteerPoints;

			// Check if team is within playoff spots
			var withinPlayoffSpots = standing.Rank <= playoffSpots;

			// Team qualifies if both conditions are met
			standing.QualifiesForPlayoffs = hasMinVolunteerPoints && withinPlayoffSpots;

			if (standing.QualifiesForPlayoffs)
			{
				qualifiedCount++;
				standing.PlayoffQualificationNote = "Clinched playoff spot";
			}
			else if (!hasMinVolunteerPoints && withinPlayoffSpots)
			{
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = needed == 1 
					? "Needs 1 more volunteer point to qualify"
					: $"Needs {needed} more volunteer points to qualify";
			}
			else if (hasMinVolunteerPoints && !withinPlayoffSpots)
			{
				standing.PlayoffQualificationNote = "Eliminated from playoffs";
			}
			else
			{
				// Neither condition met
				var needed = minVolunteerPoints - standing.VolunteerPoints;
				standing.PlayoffQualificationNote = needed == 1
					? "Needs 1 more volunteer point and must improve standing"
					: $"Needs {needed} more volunteer points and must improve standing";
			}
		}
	}

	#endregion
}
