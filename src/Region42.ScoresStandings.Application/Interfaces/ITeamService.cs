using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for managing teams including CRUD operations and validation.
/// </summary>
public interface ITeamService
{
	/// <summary>
	/// Gets all active teams for a specific division.
	/// </summary>
	Task<IEnumerable<Team>> GetTeamsByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets a team by ID.
	/// </summary>
	Task<Team?> GetTeamByIdAsync(int teamId);

	/// <summary>
	/// Gets all teams for a season.
	/// </summary>
	Task<IEnumerable<Team>> GetTeamsBySeasonAsync(int seasonId);

	/// <summary>
	/// Creates a new team.
	/// </summary>
	Task<Team> CreateTeamAsync(Team team);

	/// <summary>
	/// Updates an existing team.
	/// </summary>
	Task<Team> UpdateTeamAsync(Team team);

	/// <summary>
	/// Deactivates a team (soft delete).
	/// </summary>
	Task DeactivateTeamAsync(int teamId);

	/// <summary>
	/// Validates team name uniqueness within a division.
	/// </summary>
	Task<bool> IsTeamNameUniqueAsync(string teamName, int divisionId, int? excludeTeamId = null);
}
