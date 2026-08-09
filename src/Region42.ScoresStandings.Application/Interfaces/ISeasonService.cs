using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for managing seasons and season-related business rules.
/// </summary>
public interface ISeasonService
{
	/// <summary>
	/// Gets all seasons ordered by year descending.
	/// </summary>
	Task<IEnumerable<Season>> GetAllSeasonsAsync();

	/// <summary>
	/// Gets the currently active season.
	/// </summary>
	Task<Season?> GetActiveSeasonAsync();

	/// <summary>
	/// Gets all seasons that have no games (empty seasons available for CSV import).
	/// </summary>
	Task<IEnumerable<Season>> GetEmptySeasonsAsync();

	/// <summary>
	/// Creates a new season with the specified name.
	/// If name is not provided, uses default "Fall {currentYear}".
	/// </summary>
	Task<Season> CreateSeasonAsync(string? seasonName = null, bool setAsActive = true);

	/// <summary>
	/// Checks if a season can have its games replaced.
	/// Returns true if the season has no scores entered for Round 1 games.
	/// </summary>
	Task<bool> CanReplaceGamesAsync(int seasonId);

	/// <summary>
	/// Deletes all games (and their scores) for a specific season.
	/// Should only be called after CanReplaceGamesAsync returns true.
	/// </summary>
	Task DeleteAllGamesForSeasonAsync(int seasonId);

	/// <summary>
	/// Generates the default season name: "Fall {currentYear}".
	/// </summary>
	string GetDefaultSeasonName();

	/// <summary>
	/// Sets a season as active and deactivates all others.
	/// </summary>
	Task SetActiveSeasonAsync(int seasonId);
}

/// <summary>
/// Result of checking season availability for CSV import.
/// </summary>
public class SeasonAvailabilityResult
{
	public bool HasEmptySeasons { get; set; }
	public List<Season> EmptySeasons { get; set; } = new();
	public string DefaultSeasonName { get; set; } = string.Empty;
	public bool DefaultSeasonExists { get; set; }
	public Season? DefaultSeason { get; set; }
	public bool CanReplaceDefaultSeason { get; set; }
}
