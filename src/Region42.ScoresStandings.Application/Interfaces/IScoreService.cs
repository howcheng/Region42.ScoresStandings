using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for managing game scores including entry, updates, and validation.
/// Supports retroactive score corrections with audit trail.
/// </summary>
public interface IScoreService
{
	/// <summary>
	/// Gets the score for a specific game.
	/// </summary>
	Score? GetScoreByGameId(int gameId);

	/// <summary>
	/// Gets the score for a specific game asynchronously.
	/// </summary>
	Task<Score?> GetScoreByGameIdAsync(int gameId);

	/// <summary>
	/// Enters or updates a score for a game.
	/// Creates audit trail for corrections via CreatedAt/ModifiedAt.
	/// </summary>
	Score EnterOrUpdateScore(int gameId, int homeScore, int awayScore);

	/// <summary>
	/// Enters or updates a score for a game, optionally deferring persistence for batch operations.
	/// </summary>
	Score EnterOrUpdateScore(int gameId, int homeScore, int awayScore, bool saveChanges);

	/// <summary>
	/// Enters or updates a score for a game asynchronously.
	/// Creates audit trail for corrections via CreatedAt/ModifiedAt.
	/// </summary>
	Task<Score> EnterOrUpdateScoreAsync(int gameId, int homeScore, int awayScore);

	/// <summary>
	/// Enters or updates a score for a game asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<Score> EnterOrUpdateScoreAsync(int gameId, int homeScore, int awayScore, bool saveChanges);

	/// <summary>
	/// Enters or updates a pool play score (supports forfeits and red/yellow cards).
	/// </summary>
	PoolPlayScore EnterOrUpdatePoolPlayScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards = 0,
		int awayRedCards = 0,
		int homeYellowCards = 0,
		int awayYellowCards = 0);

	/// <summary>
	/// Enters or updates a pool play score, optionally deferring persistence for batch operations.
	/// </summary>
	PoolPlayScore EnterOrUpdatePoolPlayScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards,
		int awayRedCards,
		int homeYellowCards,
		int awayYellowCards,
		bool saveChanges);

	/// <summary>
	/// Enters or updates a pool play score asynchronously (supports forfeits and red/yellow cards).
	/// </summary>
	Task<PoolPlayScore> EnterOrUpdatePoolPlayScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards = 0,
		int awayRedCards = 0,
		int homeYellowCards = 0,
		int awayYellowCards = 0);

	/// <summary>
	/// Enters or updates a pool play score asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<PoolPlayScore> EnterOrUpdatePoolPlayScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		int homeRedCards,
		int awayRedCards,
		int homeYellowCards,
		int awayYellowCards,
		bool saveChanges);

	/// <summary>
	/// Enters or updates an elimination game score (supports forfeits and penalty shootouts).
	/// </summary>
	EliminationScore EnterOrUpdateEliminationScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore = null,
		int? awayPenaltyScore = null);

	/// <summary>
	/// Enters or updates an elimination game score, optionally deferring persistence for batch operations.
	/// </summary>
	EliminationScore EnterOrUpdateEliminationScore(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore,
		int? awayPenaltyScore,
		bool saveChanges);

	/// <summary>
	/// Enters or updates an elimination game score asynchronously (supports forfeits and penalty shootouts).
	/// </summary>
	Task<EliminationScore> EnterOrUpdateEliminationScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore = null,
		int? awayPenaltyScore = null);

	/// <summary>
	/// Enters or updates an elimination game score asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<EliminationScore> EnterOrUpdateEliminationScoreAsync(
		int gameId,
		int homeScore,
		int awayScore,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore,
		int? awayPenaltyScore,
		bool saveChanges);

	/// <summary>
	/// Gets all scores for a specific division.
	/// </summary>
	IEnumerable<Score> GetScoresByDivision(int divisionId);

	/// <summary>
	/// Gets all scores for a specific division asynchronously.
	/// </summary>
	Task<IEnumerable<Score>> GetScoresByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets all scores for games up to and including a specific round.
	/// Used for point-in-time standings calculation.
	/// </summary>
	IEnumerable<Score> GetScoresByDivisionAndRound(int divisionId, int throughRound);

	/// <summary>
	/// Gets all scores for games up to and including a specific round asynchronously.
	/// Used for point-in-time standings calculation.
	/// </summary>
	Task<IEnumerable<Score>> GetScoresByDivisionAndRoundAsync(int divisionId, int throughRound);

	/// <summary>
	/// Validates that a game is completed before allowing score entry.
	/// </summary>
	bool CanEnterScore(int gameId);

	/// <summary>
	/// Validates that a game is completed before allowing score entry asynchronously.
	/// </summary>
	Task<bool> CanEnterScoreAsync(int gameId);

	/// <summary>
	/// Deletes a score (administrative function).
	/// </summary>
	bool DeleteScore(int gameId);

	/// <summary>
	/// Deletes a score (administrative function), optionally deferring persistence for batch operations.
	/// </summary>
	bool DeleteScore(int gameId, bool saveChanges);

	/// <summary>
	/// Deletes a score asynchronously (administrative function).
	/// </summary>
	Task<bool> DeleteScoreAsync(int gameId);

	/// <summary>
	/// Deletes a score asynchronously (administrative function), optionally deferring persistence for batch operations.
	/// </summary>
	Task<bool> DeleteScoreAsync(int gameId, bool saveChanges);
}
