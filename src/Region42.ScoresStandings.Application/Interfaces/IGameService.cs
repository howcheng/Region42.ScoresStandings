using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for managing games including scheduling, rescheduling, and status updates.
/// </summary>
public interface IGameService
{
	/// <summary>
	/// Gets all games for a specific division.
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets all games for a specific round within a division.
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByDivisionAndRoundAsync(int divisionId, int round);

	/// <summary>
	/// Gets a game by ID with all related entities.
	/// </summary>
	Task<Game?> GetGameByIdAsync(int gameId);

	/// <summary>
	/// Gets all games for a specific team (home and away).
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByTeamAsync(int teamId);

	/// <summary>
	/// Creates a new game.
	/// </summary>
	Task<Game> CreateGameAsync(Game game);

	/// <summary>
	/// Updates game details (date, time, location, status).
	/// </summary>
	Task<Game> UpdateGameAsync(Game game);

	/// <summary>
	/// Updates game status.
	/// </summary>
	Task UpdateGameStatusAsync(int gameId, GameStatus status);

	/// <summary>
	/// Deletes a game if no score has been entered.
	/// </summary>
	Task<bool> DeleteGameAsync(int gameId);

	/// <summary>
	/// Validates that teams are not scheduled to play at the same time.
	/// </summary>
	Task<bool> ValidateNoScheduleConflictAsync(int teamId, DateTime scheduledDateTime, int? excludeGameId = null);
}
