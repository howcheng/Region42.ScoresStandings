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
	IEnumerable<Game> GetGamesByDivision(int divisionId);

	/// <summary>
	/// Gets all games for a specific division asynchronously.
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets all games for a specific round within a division.
	/// </summary>
	IEnumerable<Game> GetGamesByDivisionAndRound(int divisionId, int round);

	/// <summary>
	/// Gets all games for a specific round within a division asynchronously.
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByDivisionAndRoundAsync(int divisionId, int round);

	/// <summary>
	/// Gets a game by ID with all related entities.
	/// </summary>
	Game? GetGameById(int gameId);

	/// <summary>
	/// Gets a game by ID with all related entities asynchronously.
	/// </summary>
	Task<Game?> GetGameByIdAsync(int gameId);

	/// <summary>
	/// Gets all games for a specific team (home and away).
	/// </summary>
	IEnumerable<Game> GetGamesByTeam(int teamId);

	/// <summary>
	/// Gets all games for a specific team (home and away) asynchronously.
	/// </summary>
	Task<IEnumerable<Game>> GetGamesByTeamAsync(int teamId);

	/// <summary>
	/// Creates a new game.
	/// </summary>
	Game CreateGame(Game game);

	/// <summary>
	/// Creates a new game, optionally deferring persistence for batch operations.
	/// </summary>
	Game CreateGame(Game game, bool saveChanges);

	/// <summary>
	/// Creates a new game asynchronously.
	/// </summary>
	Task<Game> CreateGameAsync(Game game);

	/// <summary>
	/// Creates a new game asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<Game> CreateGameAsync(Game game, bool saveChanges);

	/// <summary>
	/// Updates game details (date, time, location, status).
	/// </summary>
	Game UpdateGame(Game game);

	/// <summary>
	/// Updates game details, optionally deferring persistence for batch operations.
	/// </summary>
	Game UpdateGame(Game game, bool saveChanges);

	/// <summary>
	/// Updates game details (date, time, location, status) asynchronously.
	/// </summary>
	Task<Game> UpdateGameAsync(Game game);

	/// <summary>
	/// Updates game details asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<Game> UpdateGameAsync(Game game, bool saveChanges);

	/// <summary>
	/// Updates game status.
	/// </summary>
	void UpdateGameStatus(int gameId, GameStatus status);

	/// <summary>
	/// Updates game status, optionally deferring persistence for batch operations.
	/// </summary>
	void UpdateGameStatus(int gameId, GameStatus status, bool saveChanges);

	/// <summary>
	/// Updates game status asynchronously.
	/// </summary>
	Task UpdateGameStatusAsync(int gameId, GameStatus status);

	/// <summary>
	/// Updates game status asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task UpdateGameStatusAsync(int gameId, GameStatus status, bool saveChanges);

	/// <summary>
	/// Deletes a game if no score has been entered.
	/// </summary>
	bool DeleteGame(int gameId);

	/// <summary>
	/// Deletes a game if no score has been entered, optionally deferring persistence for batch operations.
	/// </summary>
	bool DeleteGame(int gameId, bool saveChanges);

	/// <summary>
	/// Deletes a game if no score has been entered asynchronously.
	/// </summary>
	Task<bool> DeleteGameAsync(int gameId);

	/// <summary>
	/// Deletes a game if no score has been entered asynchronously, optionally deferring persistence for batch operations.
	/// </summary>
	Task<bool> DeleteGameAsync(int gameId, bool saveChanges);

	/// <summary>
	/// Validates that teams are not scheduled to play at the same time.
	/// </summary>
	bool ValidateNoScheduleConflict(int teamId, DateTime scheduledDateTime, int? excludeGameId = null);

	/// <summary>
	/// Validates that teams are not scheduled to play at the same time asynchronously.
	/// </summary>
	Task<bool> ValidateNoScheduleConflictAsync(int teamId, DateTime scheduledDateTime, int? excludeGameId = null);
}
