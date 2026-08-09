using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for managing volunteer points that contribute to team standings.
/// </summary>
public interface IVolunteerPointsService
{
	/// <summary>
	/// Gets all volunteer points for a specific team.
	/// </summary>
	Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByTeamAsync(int teamId);

	/// <summary>
	/// Gets volunteer points for a specific team and round.
	/// </summary>
	Task<VolunteerPoints?> GetVolunteerPointsByTeamAndRoundAsync(int teamId, int round);

	/// <summary>
	/// Gets all volunteer points for a division.
	/// </summary>
	Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAsync(int divisionId);

	/// <summary>
	/// Gets volunteer points for a division up to and including a specific round.
	/// Used for point-in-time standings calculation.
	/// </summary>
	Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAndRoundAsync(int divisionId, int throughRound);

	/// <summary>
	/// Enters or updates volunteer points for a team in a specific round.
	/// </summary>
	Task<VolunteerPoints> EnterOrUpdateVolunteerPointsAsync(int teamId, int round, int points, string notes);

	/// <summary>
	/// Deletes volunteer points entry.
	/// </summary>
	Task<bool> DeleteVolunteerPointsAsync(int volunteerPointsId);

	/// <summary>
	/// Validates that a team exists and is active.
	/// </summary>
	Task<bool> ValidateTeamAsync(int teamId);
}
