using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for managing game scores including entry, updates, validation, and audit trail.
/// Enforces business rules for score entry and maintains data integrity.
/// </summary>
public class ScoreService : IScoreService
{
	private readonly IRepository<Score> _scoreRepository;
	private readonly IRepository<Game> _gameRepository;
	private readonly ILogger<ScoreService> _logger;

	public ScoreService(
		IRepository<Score> scoreRepository,
		IRepository<Game> gameRepository,
		ILogger<ScoreService> logger)
	{
		_scoreRepository = scoreRepository;
		_gameRepository = gameRepository;
		_logger = logger;
	}

	public async Task<Score?> GetScoreByGameIdAsync(int gameId)
	{
		_logger.LogInformation("Getting score for game {GameId}", gameId);
		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		return scores.FirstOrDefault();
	}

	public async Task<Score> EnterOrUpdateScoreAsync(int gameId, int homeScore, int awayScore)
	{
		_logger.LogInformation("Entering/updating score for game {GameId}: Home={HomeScore}, Away={AwayScore}", 
			gameId, homeScore, awayScore);

		// Validate game exists
		var game = await _gameRepository.GetByIdAsync(gameId);
		if (game == null)
		{
			_logger.LogWarning("Game {GameId} not found", gameId);
			throw new ArgumentException($"Game with ID {gameId} not found", nameof(gameId));
		}

		// Validate scores are non-negative
		if (homeScore < 0)
		{
			_logger.LogWarning("Invalid home score {HomeScore} for game {GameId}", homeScore, gameId);
			throw new ArgumentException("Home score cannot be negative", nameof(homeScore));
		}

		if (awayScore < 0)
		{
			_logger.LogWarning("Invalid away score {AwayScore} for game {GameId}", awayScore, gameId);
			throw new ArgumentException("Away score cannot be negative", nameof(awayScore));
		}

		// Check if score already exists (update) or create new
		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		var existingScore = scores.FirstOrDefault();

		if (existingScore != null)
		{
			// Update existing score (audit trail via ModifiedAt/ModifiedBy in BaseEntity)
			_logger.LogInformation("Updating existing score for game {GameId}. Old: Home={OldHome}, Away={OldAway}. New: Home={NewHome}, Away={NewAway}",
				gameId, existingScore.HomeScore, existingScore.AwayScore, homeScore, awayScore);

			existingScore.HomeScore = homeScore;
			existingScore.AwayScore = awayScore;

			_scoreRepository.Update(existingScore);
			await _scoreRepository.SaveChangesAsync();
		}
		else
		{
			// Create new score
			var newScore = new Score
			{
				GameId = gameId,
				HomeScore = homeScore,
				AwayScore = awayScore
			};

			await _scoreRepository.AddAsync(newScore);
			await _scoreRepository.SaveChangesAsync();
			_logger.LogInformation("Created new score for game {GameId}", gameId);
			existingScore = newScore;
		}

		// Update game status to Completed when score is entered
		if (game.Status != GameStatus.Completed)
		{
			game.Status = GameStatus.Completed;
			_gameRepository.Update(game);
			await _gameRepository.SaveChangesAsync();
			_logger.LogInformation("Game {GameId} status updated to Completed", gameId);
		}

		return existingScore;
	}

	public async Task<IEnumerable<Score>> GetScoresByDivisionAsync(int divisionId)
	{
		_logger.LogInformation("Getting all scores for division {DivisionId}", divisionId);

		var scores = await _scoreRepository.FindAsync(score => 
			score.Game.DivisionId == divisionId);

		return scores;
	}

	public async Task<IEnumerable<Score>> GetScoresByDivisionAndRoundAsync(int divisionId, int throughRound)
	{
		_logger.LogInformation("Getting scores for division {DivisionId} through round {Round}", 
			divisionId, throughRound);

		var scores = await _scoreRepository.FindAsync(score => 
			score.Game.DivisionId == divisionId && 
			score.Game.Round <= throughRound);

		return scores;
	}

	public async Task<bool> CanEnterScoreAsync(int gameId)
	{
		_logger.LogDebug("Checking if score can be entered for game {GameId}", gameId);

		var game = await _gameRepository.GetByIdAsync(gameId);

		if (game == null)
		{
			_logger.LogWarning("Game {GameId} not found", gameId);
			return false;
		}

		var canEnter = game.Status == GameStatus.Completed;

		if (!canEnter)
		{
			_logger.LogDebug("Cannot enter score for game {GameId} - status is {Status}", gameId, game.Status);
		}

		return canEnter;
	}

	public async Task<bool> DeleteScoreAsync(int gameId)
	{
		_logger.LogInformation("Deleting score for game {GameId}", gameId);

		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		var score = scores.FirstOrDefault();

		if (score == null)
		{
			_logger.LogWarning("Score for game {GameId} not found", gameId);
			return false;
		}

		_scoreRepository.Delete(score);
		await _scoreRepository.SaveChangesAsync();
		_logger.LogInformation("Successfully deleted score for game {GameId}", gameId);

		// Revert game status back to Scheduled when score is deleted
		var game = await _gameRepository.GetByIdAsync(gameId);
		if (game != null && game.Status == GameStatus.Completed)
		{
			game.Status = GameStatus.Scheduled;
			_gameRepository.Update(game);
			await _gameRepository.SaveChangesAsync();
			_logger.LogInformation("Game {GameId} status reverted to Scheduled after score deletion", gameId);
		}

		return true;
	}
}
