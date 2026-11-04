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
	private readonly IStandingsRefreshService _standingsRefreshService;
	private readonly ILogger<ScoreService> _logger;

	public ScoreService(
		IRepository<Score> scoreRepository,
		IRepository<Game> gameRepository,
		IStandingsRefreshService standingsRefreshService,
		ILogger<ScoreService> logger)
	{
		_scoreRepository = scoreRepository;
		_gameRepository = gameRepository;
		_standingsRefreshService = standingsRefreshService;
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

		var game = await _gameRepository.GetByIdAsync(gameId);
		if (game == null)
		{
			_logger.LogWarning("Game {GameId} not found", gameId);
			throw new ArgumentException($"Game with ID {gameId} not found", nameof(gameId));
		}

		ValidateNonNegativeScores(gameId, homeScore, awayScore);

		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		var existingScore = scores.FirstOrDefault();

		if (existingScore != null)
		{
			_logger.LogInformation("Updating existing score for game {GameId}. Old: Home={OldHome}, Away={OldAway}. New: Home={NewHome}, Away={NewAway}",
				gameId, existingScore.HomeScore, existingScore.AwayScore, homeScore, awayScore);

			existingScore.HomeScore = homeScore;
			existingScore.AwayScore = awayScore;

			_scoreRepository.Update(existingScore);
		}
		else
		{
			var newScore = new Score
			{
				GameId = gameId,
				HomeScore = homeScore,
				AwayScore = awayScore
			};

			await _scoreRepository.AddAsync(newScore);
			_logger.LogInformation("Created new score for game {GameId}", gameId);
			existingScore = newScore;
		}

		if (game.Status != GameStatus.Completed)
		{
			game.Status = GameStatus.Completed;
			_gameRepository.Update(game);
		}

		existingScore.Game = game;
		game.Score = existingScore;
		await RefreshStandingsAndSaveAsync(game.DivisionId);
		_logger.LogInformation("Game {GameId} score saved and standings refreshed", gameId);

		return existingScore;
	}

	public PoolPlayScore EnterOrUpdatePoolPlayScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards = 0,
		int awayRedCards = 0,
		int homeYellowCards = 0,
		int awayYellowCards = 0)
	{
		ValidatePoolPlayScoreInputs(gameId, homeScore, awayScore, isForfeit, homeRedCards, awayRedCards, homeYellowCards, awayYellowCards);
		return EnterOrUpdatePoolPlayScoreInternalAsync(
			gameId,
			homeScore,
			awayScore,
			isForfeit,
			homeRedCards,
			awayRedCards,
			homeYellowCards,
			awayYellowCards).GetAwaiter().GetResult();
	}

	public Task<PoolPlayScore> EnterOrUpdatePoolPlayScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards = 0,
		int awayRedCards = 0,
		int homeYellowCards = 0,
		int awayYellowCards = 0)
	{
		ValidatePoolPlayScoreInputs(gameId, homeScore, awayScore, isForfeit, homeRedCards, awayRedCards, homeYellowCards, awayYellowCards);
		return EnterOrUpdatePoolPlayScoreInternalAsync(
			gameId,
			homeScore,
			awayScore,
			isForfeit,
			homeRedCards,
			awayRedCards,
			homeYellowCards,
			awayYellowCards);
	}

	private void ValidatePoolPlayScoreInputs(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards,
		int awayRedCards,
		int homeYellowCards,
		int awayYellowCards)
	{
		ValidateNonNegativeScores(gameId, homeScore, awayScore);

		if (homeRedCards < 0 || awayRedCards < 0 || homeYellowCards < 0 || awayYellowCards < 0)
		{
			throw new ArgumentException("Red and yellow card counts cannot be negative.");
		}

		if (isForfeit && !((homeScore == 1 && awayScore == 0) || (homeScore == 0 && awayScore == 1)))
		{
			throw new ArgumentException("Forfeit games must be scored as 1-0 or 0-1.");
		}
	}

	private async Task<PoolPlayScore> EnterOrUpdatePoolPlayScoreInternalAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards,
		int awayRedCards,
		int homeYellowCards,
		int awayYellowCards)
	{
		var game = await _gameRepository.GetByIdAsync(gameId)
			?? throw new ArgumentException($"Game with ID {gameId} not found", nameof(gameId));

		if (game is not PoolPlayGame)
		{
			throw new InvalidOperationException($"Game {gameId} is not a pool play game.");
		}

		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		var existing = scores.FirstOrDefault();

		PoolPlayScore poolScore;
		if (existing is PoolPlayScore existingPoolScore)
		{
			existingPoolScore.HomeScore = homeScore;
			existingPoolScore.AwayScore = awayScore;
			existingPoolScore.IsForfeit = isForfeit;
			existingPoolScore.HomeRedCards = homeRedCards;
			existingPoolScore.AwayRedCards = awayRedCards;
			existingPoolScore.HomeYellowCards = homeYellowCards;
			existingPoolScore.AwayYellowCards = awayYellowCards;
			_scoreRepository.Update(existingPoolScore);
			poolScore = existingPoolScore;
		}
		else
		{
			if (existing != null)
			{
				_scoreRepository.Delete(existing);
			}

			poolScore = new PoolPlayScore
			{
				GameId = gameId,
				HomeScore = homeScore,
				AwayScore = awayScore,
				IsForfeit = isForfeit,
				HomeRedCards = homeRedCards,
				AwayRedCards = awayRedCards,
				HomeYellowCards = homeYellowCards,
				AwayYellowCards = awayYellowCards
			};
			await _scoreRepository.AddAsync(poolScore);
		}

		if (game.Status != GameStatus.Completed)
		{
			game.Status = GameStatus.Completed;
			_gameRepository.Update(game);
		}

		poolScore.Game = game;
		game.Score = poolScore;
		await RefreshStandingsAndSaveAsync(game.DivisionId);

		return poolScore;
	}

	public EliminationScore EnterOrUpdateEliminationScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore = null,
		int? awayPenaltyScore = null)
	{
		ValidateEliminationScoreInputs(gameId, homeScore, awayScore, isForfeit, isDecidedByPenalties, homePenaltyScore, awayPenaltyScore);
		return EnterOrUpdateEliminationScoreInternalAsync(
			gameId,
			homeScore,
			awayScore,
			isForfeit,
			isDecidedByPenalties,
			homePenaltyScore,
			awayPenaltyScore).GetAwaiter().GetResult();
	}

	public Task<EliminationScore> EnterOrUpdateEliminationScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore = null,
		int? awayPenaltyScore = null)
	{
		ValidateEliminationScoreInputs(gameId, homeScore, awayScore, isForfeit, isDecidedByPenalties, homePenaltyScore, awayPenaltyScore);
		return EnterOrUpdateEliminationScoreInternalAsync(
			gameId,
			homeScore,
			awayScore,
			isForfeit,
			isDecidedByPenalties,
			homePenaltyScore,
			awayPenaltyScore);
	}

	private void ValidateEliminationScoreInputs(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore,
		int? awayPenaltyScore)
	{
		ValidateNonNegativeScores(gameId, homeScore, awayScore);

		if (isForfeit)
		{
			if (!((homeScore == 1 && awayScore == 0) || (homeScore == 0 && awayScore == 1)))
			{
				throw new ArgumentException("Forfeit games must be scored as 1-0 or 0-1.");
			}

			if (isDecidedByPenalties || homePenaltyScore.HasValue || awayPenaltyScore.HasValue)
			{
				throw new ArgumentException("Forfeit games cannot also have penalty shootout scores.");
			}

			return;
		}

		if (homeScore == awayScore)
		{
			if (!homePenaltyScore.HasValue || !awayPenaltyScore.HasValue)
			{
				throw new ArgumentException("Elimination games that end in a tie in regulation require both penalty shootout scores.");
			}

			if (homePenaltyScore.Value < 0 || awayPenaltyScore.Value < 0)
			{
				throw new ArgumentException("Penalty shootout scores cannot be negative.");
			}

			if (homePenaltyScore.Value == awayPenaltyScore.Value)
			{
				throw new ArgumentException("Penalty shootout scores cannot be tied; a winner must be decided.");
			}
		}
		else if (isDecidedByPenalties || homePenaltyScore.HasValue || awayPenaltyScore.HasValue)
		{
			throw new ArgumentException("Penalty shootout scores can only be entered when regulation ends in a tie.");
		}
	}

	private async Task<EliminationScore> EnterOrUpdateEliminationScoreInternalAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore,
		int? awayPenaltyScore)
	{
		var game = await _gameRepository.GetByIdAsync(gameId)
			?? throw new ArgumentException($"Game with ID {gameId} not found", nameof(gameId));

		if (game is not EliminationGame)
		{
			throw new InvalidOperationException($"Game {gameId} is not an elimination game.");
		}

		var decidedByPenalties = !isForfeit && homeScore == awayScore && homePenaltyScore.HasValue && awayPenaltyScore.HasValue;

		var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
		var existing = scores.FirstOrDefault();

		EliminationScore elimScore;
		if (existing is EliminationScore existingElimScore)
		{
			existingElimScore.HomeScore = homeScore;
			existingElimScore.AwayScore = awayScore;
			existingElimScore.IsForfeit = isForfeit;
			existingElimScore.IsDecidedByPenalties = decidedByPenalties;
			existingElimScore.HomePenaltyScore = decidedByPenalties ? homePenaltyScore : null;
			existingElimScore.AwayPenaltyScore = decidedByPenalties ? awayPenaltyScore : null;
			_scoreRepository.Update(existingElimScore);
			elimScore = existingElimScore;
		}
		else
		{
			if (existing != null)
			{
				_scoreRepository.Delete(existing);
			}

			elimScore = new EliminationScore
			{
				GameId = gameId,
				HomeScore = homeScore,
				AwayScore = awayScore,
				IsForfeit = isForfeit,
				IsDecidedByPenalties = decidedByPenalties,
				HomePenaltyScore = decidedByPenalties ? homePenaltyScore : null,
				AwayPenaltyScore = decidedByPenalties ? awayPenaltyScore : null
			};
			await _scoreRepository.AddAsync(elimScore);
		}

		if (game.Status != GameStatus.Completed)
		{
			game.Status = GameStatus.Completed;
			_gameRepository.Update(game);
		}

		elimScore.Game = game;
		game.Score = elimScore;
		await RefreshStandingsAndSaveAsync(game.DivisionId);

		return elimScore;
	}

	private void ValidateNonNegativeScores(int gameId, int homeScore, int awayScore)
	{
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
	}

	private async Task RefreshStandingsAndSaveAsync(int divisionId)
	{
		await _standingsRefreshService.RefreshDivisionStandingsAsync(divisionId);
		await _scoreRepository.SaveChangesAsync();
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

		var game = await _gameRepository.GetByIdAsync(gameId);
		_scoreRepository.Delete(score);

		if (game != null && game.Status == GameStatus.Completed)
		{
			game.Status = game.ScheduledDateTime == default ? GameStatus.Unscheduled : GameStatus.Scheduled;
			game.Score = null;
			_gameRepository.Update(game);
		}

		if (game != null)
		{
			await RefreshStandingsAndSaveAsync(game.DivisionId);
		}
		else
		{
			await _scoreRepository.SaveChangesAsync();
		}

		_logger.LogInformation("Successfully deleted score for game {GameId}", gameId);
		return true;
	}
}
