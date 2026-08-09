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
	Task<Score?> GetScoreByGameIdAsync(int gameId);

	/// <summary>
	/// Enters or updates a score for a game.
	/// Creates audit trail for corrections via CreatedAt/ModifiedAt.
	/// </summary>
	Task<Score> EnterOrUpdateScoreAsync(int gameId, int homeScore, int awayScore);

	/// <summary>
	/// Gets all scores for a specific division.
	/// </summary>
	Task<IEnumerable<Score>> GetScoresByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets all scores for games up to and including a specific round.
	/// Used for point-in-time standings calculation.
	/// </summary>
	Task<IEnumerable<Score>> GetScoresByDivisionAndRoundAsync(int divisionId, int throughRound);

	/// <summary>
	/// Validates that a game is completed before allowing score entry.
	/// </summary>
	Task<bool> CanEnterScoreAsync(int gameId);

	/// <summary>
	/// Deletes a score (administrative function).
	/// </summary>
	Task<bool> DeleteScoreAsync(int gameId);
}
