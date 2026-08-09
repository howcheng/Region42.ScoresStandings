using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for managing teams with validation and business rules.
/// Ensures team name uniqueness within divisions and prevents deletion of teams with games/scores.
/// </summary>
public class TeamService : ITeamService
{
	private readonly IRepository<Team> _teamRepository;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Game> _gameRepository;
	private readonly ILogger<TeamService> _logger;

	public TeamService(
		IRepository<Team> teamRepository,
		IRepository<Division> divisionRepository,
		IRepository<Game> gameRepository,
		ILogger<TeamService> logger)
	{
		_teamRepository = teamRepository;
		_divisionRepository = divisionRepository;
		_gameRepository = gameRepository;
		_logger = logger;
	}

	public async Task<IEnumerable<Team>> GetTeamsByDivisionAsync(int divisionId)
	{
		_logger.LogInformation("Getting teams for division {DivisionId}", divisionId);
		return await _teamRepository.FindAsync(t => t.DivisionId == divisionId && t.IsActive);
	}

	public async Task<Team?> GetTeamByIdAsync(int teamId)
	{
		_logger.LogDebug("Getting team {TeamId}", teamId);
		return await _teamRepository.GetByIdAsync(teamId);
	}

	public async Task<IEnumerable<Team>> GetTeamsBySeasonAsync(int seasonId)
	{
		_logger.LogInformation("Getting teams for season {SeasonId}", seasonId);

		// Get all divisions for the season
		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == seasonId);
		var divisionIds = divisions.Select(d => d.Id).ToHashSet();

		// Get all active teams for those divisions
		var teams = await _teamRepository.FindAsync(t => divisionIds.Contains(t.DivisionId) && t.IsActive);
		return teams;
	}

	public async Task<Team> CreateTeamAsync(Team team)
	{
		_logger.LogInformation("Creating team {TeamName} in division {DivisionId}", team.Name, team.DivisionId);

		// Validate division exists
		var division = await _divisionRepository.GetByIdAsync(team.DivisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", team.DivisionId);
			throw new ArgumentException($"Division with ID {team.DivisionId} does not exist.", nameof(team.DivisionId));
		}

		// Validate team name uniqueness within division
		if (!await IsTeamNameUniqueAsync(team.Name, team.DivisionId))
		{
			_logger.LogWarning("Team name {TeamName} already exists in division {DivisionId}", team.Name, team.DivisionId);
			throw new InvalidOperationException($"Team name '{team.Name}' already exists in this division.");
		}

		// Ensure team is active by default
		team.IsActive = true;

		await _teamRepository.AddAsync(team);
		await _teamRepository.SaveChangesAsync();

		_logger.LogInformation("Team {TeamId} created successfully", team.Id);
		return team;
	}

	public async Task<Team> UpdateTeamAsync(Team team)
	{
		_logger.LogInformation("Updating team {TeamId}", team.Id);

		// Verify team exists
		var existingTeam = await _teamRepository.GetByIdAsync(team.Id);
		if (existingTeam == null)
		{
			_logger.LogWarning("Team {TeamId} not found", team.Id);
			throw new ArgumentException($"Team with ID {team.Id} not found.", nameof(team.Id));
		}

		// Validate division exists
		var division = await _divisionRepository.GetByIdAsync(team.DivisionId);
		if (division == null)
		{
			_logger.LogWarning("Division {DivisionId} not found", team.DivisionId);
			throw new ArgumentException($"Division with ID {team.DivisionId} does not exist.", nameof(team.DivisionId));
		}

		// Validate team name uniqueness (excluding current team)
		if (!await IsTeamNameUniqueAsync(team.Name, team.DivisionId, team.Id))
		{
			_logger.LogWarning("Team name {TeamName} already exists in division {DivisionId}", team.Name, team.DivisionId);
			throw new InvalidOperationException($"Team name '{team.Name}' already exists in this division.");
		}

		_teamRepository.Update(team);
		await _teamRepository.SaveChangesAsync();

		_logger.LogInformation("Team {TeamId} updated successfully", team.Id);
		return team;
	}

	public async Task DeactivateTeamAsync(int teamId)
	{
		_logger.LogInformation("Deactivating team {TeamId}", teamId);

		var team = await _teamRepository.GetByIdAsync(teamId);
		if (team == null)
		{
			_logger.LogWarning("Team {TeamId} not found", teamId);
			throw new ArgumentException($"Team with ID {teamId} not found.", nameof(teamId));
		}

		// Check if team has any associated games
		var hasGames = (await _gameRepository.FindAsync(g => g.HomeTeamId == teamId || g.AwayTeamId == teamId)).Any();
		if (hasGames)
		{
			_logger.LogWarning("Cannot deactivate team {TeamId} - has associated games", teamId);
			throw new InvalidOperationException($"Cannot deactivate team '{team.Name}' because it has associated games. Teams with game history should remain active for historical records.");
		}

		// Soft delete by marking inactive
		team.IsActive = false;
		_teamRepository.Update(team);
		await _teamRepository.SaveChangesAsync();

		_logger.LogInformation("Team {TeamId} deactivated successfully", teamId);
	}

	public async Task<bool> IsTeamNameUniqueAsync(string teamName, int divisionId, int? excludeTeamId = null)
	{
		_logger.LogDebug("Checking uniqueness of team name {TeamName} in division {DivisionId}", teamName, divisionId);

		var existingTeams = await _teamRepository.FindAsync(t => 
			t.Name.ToLower() == teamName.ToLower() && 
			t.DivisionId == divisionId &&
			t.IsActive);

		if (excludeTeamId.HasValue)
		{
			existingTeams = existingTeams.Where(t => t.Id != excludeTeamId.Value);
		}

		return !existingTeams.Any();
	}
}
