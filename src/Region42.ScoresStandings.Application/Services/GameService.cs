using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for managing games with comprehensive validation and business rules.
/// Ensures data integrity for game scheduling, team assignments, and score relationships.
/// </summary>
public class GameService : IGameService
{
	private readonly IRepository<Game> _gameRepository;
	private readonly IRepository<Team> _teamRepository;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Score> _scoreRepository;
	private readonly ILogger<GameService> _logger;

	public GameService(
		IRepository<Game> gameRepository,
		IRepository<Team> teamRepository,
		IRepository<Division> divisionRepository,
		IRepository<Score> scoreRepository,
		ILogger<GameService> logger)
	{
		_gameRepository = gameRepository;
		_teamRepository = teamRepository;
		_divisionRepository = divisionRepository;
		_scoreRepository = scoreRepository;
		_logger = logger;
	}

	public async Task<IEnumerable<Game>> GetGamesByDivisionAsync(int divisionId)
	{
		_logger.LogInformation("Getting games for division {DivisionId}", divisionId);
		return await _gameRepository.FindAsync(g => g.DivisionId == divisionId);
	}

	public async Task<IEnumerable<Game>> GetGamesByDivisionAndRoundAsync(int divisionId, int round)
	{
		_logger.LogInformation("Getting games for division {DivisionId}, round {Round}", divisionId, round);
		return await _gameRepository.FindAsync(g => g.DivisionId == divisionId && g.Round == round);
	}

	public async Task<Game?> GetGameByIdAsync(int gameId)
	{
		_logger.LogDebug("Getting game {GameId}", gameId);
		return await _gameRepository.GetByIdAsync(gameId);
	}

	public async Task<IEnumerable<Game>> GetGamesByTeamAsync(int teamId)
	{
		_logger.LogInformation("Getting games for team {TeamId}", teamId);
		return await _gameRepository.FindAsync(g => g.HomeTeamId == teamId || g.AwayTeamId == teamId);
	}

	public async Task<Game> CreateGameAsync(Game game)
	{
		_logger.LogInformation("Creating game between teams {HomeTeamId} and {AwayTeamId}", 
			game.HomeTeamId, game.AwayTeamId);

		// Validate division exists
		var division = await _divisionRepository.GetByIdAsync(game.DivisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", game.DivisionId);
			throw new ArgumentException($"Division with ID {game.DivisionId} does not exist.", nameof(game.DivisionId));
		}

		// Validate home team exists and belongs to division
		var homeTeam = await _teamRepository.GetByIdAsync(game.HomeTeamId);
		if (homeTeam == null)
		{
			_logger.LogWarning("Home team {TeamId} not found", game.HomeTeamId);
			throw new ArgumentException($"Home team with ID {game.HomeTeamId} does not exist.", nameof(game.HomeTeamId));
		}

		if (homeTeam.DivisionId != game.DivisionId)
		{
			_logger.LogWarning("Home team {TeamId} does not belong to division {DivisionId}", 
				game.HomeTeamId, game.DivisionId);
			throw new InvalidOperationException($"Home team '{homeTeam.Name}' does not belong to the specified division.");
		}

		// Validate away team exists and belongs to division
		var awayTeam = await _teamRepository.GetByIdAsync(game.AwayTeamId);
		if (awayTeam == null)
		{
			_logger.LogWarning("Away team {TeamId} not found", game.AwayTeamId);
			throw new ArgumentException($"Away team with ID {game.AwayTeamId} does not exist.", nameof(game.AwayTeamId));
		}

		if (awayTeam.DivisionId != game.DivisionId)
		{
			_logger.LogWarning("Away team {TeamId} does not belong to division {DivisionId}", 
				game.AwayTeamId, game.DivisionId);
			throw new InvalidOperationException($"Away team '{awayTeam.Name}' does not belong to the specified division.");
		}

		// Validate team doesn't play itself
		if (game.HomeTeamId == game.AwayTeamId)
		{
			_logger.LogWarning("Team {TeamId} cannot play against itself", game.HomeTeamId);
			throw new InvalidOperationException("A team cannot play against itself.");
		}

		// Validate round number
		if (game.Round < 1 || game.Round > division.TotalRounds)
		{
			_logger.LogWarning("Invalid round number {Round} for division with {TotalRounds} rounds", 
				game.Round, division.TotalRounds);
			throw new ArgumentException($"Round must be between 1 and {division.TotalRounds}.", nameof(game.Round));
		}

		// Validate scheduled date/time
		if (game.ScheduledDateTime < DateTime.UtcNow.AddDays(-1))
		{
			_logger.LogWarning("Game scheduled in the past: {ScheduledDateTime}", game.ScheduledDateTime);
			throw new ArgumentException("Game cannot be scheduled in the past.", nameof(game.ScheduledDateTime));
		}

		// Check for schedule conflicts
		if (!await ValidateNoScheduleConflictAsync(game.HomeTeamId, game.ScheduledDateTime))
		{
			_logger.LogWarning("Home team {TeamId} has scheduling conflict at {ScheduledDateTime}", 
				game.HomeTeamId, game.ScheduledDateTime);
			throw new InvalidOperationException($"Home team '{homeTeam.Name}' is already scheduled to play at {game.ScheduledDateTime:g}.");
		}

		if (!await ValidateNoScheduleConflictAsync(game.AwayTeamId, game.ScheduledDateTime))
		{
			_logger.LogWarning("Away team {TeamId} has scheduling conflict at {ScheduledDateTime}", 
				game.AwayTeamId, game.ScheduledDateTime);
			throw new InvalidOperationException($"Away team '{awayTeam.Name}' is already scheduled to play at {game.ScheduledDateTime:g}.");
		}

		// Set default status if not provided
		if (game.Status == default)
		{
			game.Status = GameStatus.Scheduled;
		}

		await _gameRepository.AddAsync(game);
		await _gameRepository.SaveChangesAsync();

		_logger.LogInformation("Game {GameId} created successfully", game.Id);
		return game;
	}

	public async Task<Game> UpdateGameAsync(Game game)
	{
		_logger.LogInformation("Updating game {GameId}", game.Id);

		// Verify game exists
		var existingGame = await _gameRepository.GetByIdAsync(game.Id);
		if (existingGame == null)
		{
			_logger.LogWarning("Game {GameId} not found", game.Id);
			throw new ArgumentException($"Game with ID {game.Id} not found.", nameof(game.Id));
		}

		// Validate division exists
		var division = await _divisionRepository.GetByIdAsync(game.DivisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", game.DivisionId);
			throw new ArgumentException($"Division with ID {game.DivisionId} does not exist.", nameof(game.DivisionId));
		}

		// Validate home team
		var homeTeam = await _teamRepository.GetByIdAsync(game.HomeTeamId);
		if (homeTeam == null)
		{
			_logger.LogWarning("Home team {TeamId} not found", game.HomeTeamId);
			throw new ArgumentException($"Home team with ID {game.HomeTeamId} does not exist.", nameof(game.HomeTeamId));
		}

		if (homeTeam.DivisionId != game.DivisionId)
		{
			_logger.LogWarning("Home team {TeamId} does not belong to division {DivisionId}", 
				game.HomeTeamId, game.DivisionId);
			throw new InvalidOperationException($"Home team '{homeTeam.Name}' does not belong to the specified division.");
		}

		// Validate away team
		var awayTeam = await _teamRepository.GetByIdAsync(game.AwayTeamId);
		if (awayTeam == null)
		{
			_logger.LogWarning("Away team {TeamId} not found", game.AwayTeamId);
			throw new ArgumentException($"Away team with ID {game.AwayTeamId} does not exist.", nameof(game.AwayTeamId));
		}

		if (awayTeam.DivisionId != game.DivisionId)
		{
			_logger.LogWarning("Away team {TeamId} does not belong to division {DivisionId}", 
				game.AwayTeamId, game.DivisionId);
			throw new InvalidOperationException($"Away team '{awayTeam.Name}' does not belong to the specified division.");
		}

		// Validate team doesn't play itself
		if (game.HomeTeamId == game.AwayTeamId)
		{
			_logger.LogWarning("Team {TeamId} cannot play against itself", game.HomeTeamId);
			throw new InvalidOperationException("A team cannot play against itself.");
		}

		// Validate round number
		if (game.Round < 1 || game.Round > division.TotalRounds)
		{
			_logger.LogWarning("Invalid round number {Round} for division with {TotalRounds} rounds", 
				game.Round, division.TotalRounds);
			throw new ArgumentException($"Round must be between 1 and {division.TotalRounds}.", nameof(game.Round));
		}

		// Check for schedule conflicts (excluding this game)
		if (!await ValidateNoScheduleConflictAsync(game.HomeTeamId, game.ScheduledDateTime, game.Id))
		{
			_logger.LogWarning("Home team {TeamId} has scheduling conflict at {ScheduledDateTime}", 
				game.HomeTeamId, game.ScheduledDateTime);
			throw new InvalidOperationException($"Home team '{homeTeam.Name}' is already scheduled to play at {game.ScheduledDateTime:g}.");
		}

		if (!await ValidateNoScheduleConflictAsync(game.AwayTeamId, game.ScheduledDateTime, game.Id))
		{
			_logger.LogWarning("Away team {TeamId} has scheduling conflict at {ScheduledDateTime}", 
				game.AwayTeamId, game.ScheduledDateTime);
			throw new InvalidOperationException($"Away team '{awayTeam.Name}' is already scheduled to play at {game.ScheduledDateTime:g}.");
		}

		_gameRepository.Update(game);
		await _gameRepository.SaveChangesAsync();

		_logger.LogInformation("Game {GameId} updated successfully", game.Id);
		return game;
	}

	public async Task UpdateGameStatusAsync(int gameId, GameStatus status)
	{
		_logger.LogInformation("Updating status of game {GameId} to {Status}", gameId, status);

		var game = await _gameRepository.GetByIdAsync(gameId);
		if (game == null)
		{
			_logger.LogWarning("Game {GameId} not found", gameId);
			throw new ArgumentException($"Game with ID {gameId} not found.", nameof(gameId));
		}

		game.Status = status;
		_gameRepository.Update(game);
		await _gameRepository.SaveChangesAsync();

		_logger.LogInformation("Game {GameId} status updated to {Status} successfully", gameId, status);
	}

	public async Task<bool> DeleteGameAsync(int gameId)
	{
		_logger.LogInformation("Attempting to delete game {GameId}", gameId);

		var game = await _gameRepository.GetByIdAsync(gameId);
		if (game == null)
		{
			_logger.LogWarning("Game {GameId} not found", gameId);
			throw new ArgumentException($"Game with ID {gameId} not found.", nameof(gameId));
		}

		// Check if game has a score
		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		if (scores.Any())
		{
			_logger.LogWarning("Cannot delete game {GameId} - score exists", gameId);
			throw new InvalidOperationException($"Cannot delete game {gameId} because a score has been entered. Games with scores cannot be deleted to preserve historical data.");
		}

		_gameRepository.Delete(game);
		await _gameRepository.SaveChangesAsync();

		_logger.LogInformation("Game {GameId} deleted successfully", gameId);
		return true;
	}

	public async Task<bool> ValidateNoScheduleConflictAsync(int teamId, DateTime scheduledDateTime, int? excludeGameId = null)
	{
		_logger.LogDebug("Checking schedule conflicts for team {TeamId} at {ScheduledDateTime}", 
			teamId, scheduledDateTime);

		// Check for games within 2 hours of scheduled time (buffer for travel, warm-up, etc.)
		var conflictWindow = TimeSpan.FromHours(2);
		var startWindow = scheduledDateTime.AddHours(-conflictWindow.TotalHours);
		var endWindow = scheduledDateTime.AddHours(conflictWindow.TotalHours);

		var conflictingGames = await _gameRepository.FindAsync(g =>
			(g.HomeTeamId == teamId || g.AwayTeamId == teamId) &&
			g.Status != GameStatus.Cancelled &&
			g.ScheduledDateTime >= startWindow &&
			g.ScheduledDateTime <= endWindow);

		if (excludeGameId.HasValue)
		{
			conflictingGames = conflictingGames.Where(g => g.Id != excludeGameId.Value);
		}

		return !conflictingGames.Any();
	}
}
