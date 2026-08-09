using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for managing volunteer points that contribute to team standings.
/// Supports bulk entry via grid UI and point-in-time queries.
/// </summary>
public class VolunteerPointsService : IVolunteerPointsService
{
	private readonly IRepository<VolunteerPoints> _volunteerPointsRepository;
	private readonly IRepository<Team> _teamRepository;
	private readonly ILogger<VolunteerPointsService> _logger;

	public VolunteerPointsService(
		IRepository<VolunteerPoints> volunteerPointsRepository,
		IRepository<Team> teamRepository,
		ILogger<VolunteerPointsService> logger)
	{
		_volunteerPointsRepository = volunteerPointsRepository;
		_teamRepository = teamRepository;
		_logger = logger;
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByTeamAsync(int teamId)
	{
		_logger.LogInformation("Getting all volunteer points for team {TeamId}", teamId);

		var points = await _volunteerPointsRepository.FindAsync(vp => vp.TeamId == teamId);
		return points;
	}

	public async Task<VolunteerPoints?> GetVolunteerPointsByTeamAndRoundAsync(int teamId, int round)
	{
		_logger.LogInformation("Getting volunteer points for team {TeamId}, round {Round}", teamId, round);

		var points = await _volunteerPointsRepository.FindAsync(vp => 
			vp.TeamId == teamId && vp.Round == round);

		return points.FirstOrDefault();
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAsync(int divisionId)
	{
		_logger.LogInformation("Getting all volunteer points for division {DivisionId}", divisionId);

		var points = await _volunteerPointsRepository.FindAsync(vp => 
			vp.Team.DivisionId == divisionId);

		return points;
	}

	public async Task<IEnumerable<VolunteerPoints>> GetVolunteerPointsByDivisionAndRoundAsync(int divisionId, int throughRound)
	{
		_logger.LogInformation("Getting volunteer points for division {DivisionId} through round {Round}", 
			divisionId, throughRound);

		var points = await _volunteerPointsRepository.FindAsync(vp => 
			vp.Team.DivisionId == divisionId && vp.Round <= throughRound);

		return points;
	}

	public async Task<VolunteerPoints> EnterOrUpdateVolunteerPointsAsync(int teamId, int round, int points, string notes)
	{
		_logger.LogInformation("Entering/updating volunteer points for team {TeamId}, round {Round}: Points={Points}", 
			teamId, round, points);

		// Validate team exists and is active
		var team = await _teamRepository.GetByIdAsync(teamId);
		if (team == null)
		{
			_logger.LogWarning("Team {TeamId} not found", teamId);
			throw new ArgumentException($"Team with ID {teamId} not found", nameof(teamId));
		}

		if (!team.IsActive)
		{
			_logger.LogWarning("Team {TeamId} is not active", teamId);
			throw new InvalidOperationException($"Cannot assign volunteer points to inactive team {teamId}");
		}

		// Validate round is positive
		if (round < 1)
		{
			_logger.LogWarning("Invalid round {Round} for team {TeamId}", round, teamId);
			throw new ArgumentException("Round must be greater than 0", nameof(round));
		}

		// Validate points are non-negative
		if (points < 0)
		{
			_logger.LogWarning("Invalid points {Points} for team {TeamId}, round {Round}", points, teamId, round);
			throw new ArgumentException("Points cannot be negative", nameof(points));
		}

		// Check if entry already exists for this team and round
		var existingPoints = await _volunteerPointsRepository.FindAsync(vp => 
			vp.TeamId == teamId && vp.Round == round);

		var existing = existingPoints.FirstOrDefault();

		if (existing != null)
		{
			// Update existing entry
			_logger.LogInformation("Updating volunteer points for team {TeamId}, round {Round}. Old: {OldPoints}, New: {NewPoints}",
				teamId, round, existing.Points, points);

			existing.Points = points;
			existing.Notes = notes;

			_volunteerPointsRepository.Update(existing);
			await _volunteerPointsRepository.SaveChangesAsync();
			return existing;
		}
		else
		{
			// Create new entry
			var newPoints = new VolunteerPoints
			{
				TeamId = teamId,
				Round = round,
				Points = points,
				Notes = notes
			};

			await _volunteerPointsRepository.AddAsync(newPoints);
			await _volunteerPointsRepository.SaveChangesAsync();

			_logger.LogInformation("Created volunteer points entry for team {TeamId}, round {Round}", teamId, round);
			return newPoints;
		}
	}

	public async Task<bool> DeleteVolunteerPointsAsync(int volunteerPointsId)
	{
		_logger.LogInformation("Deleting volunteer points {VolunteerPointsId}", volunteerPointsId);

		var points = await _volunteerPointsRepository.GetByIdAsync(volunteerPointsId);

		if (points == null)
		{
			_logger.LogWarning("Volunteer points {VolunteerPointsId} not found", volunteerPointsId);
			return false;
		}

		_volunteerPointsRepository.Delete(points);
		await _volunteerPointsRepository.SaveChangesAsync();

		_logger.LogInformation("Successfully deleted volunteer points {VolunteerPointsId}", volunteerPointsId);
		return true;
	}

	public async Task<bool> ValidateTeamAsync(int teamId)
	{
		_logger.LogDebug("Validating team {TeamId}", teamId);

		var team = await _teamRepository.GetByIdAsync(teamId);

		if (team == null)
		{
			_logger.LogDebug("Team {TeamId} not found", teamId);
			return false;
		}

		if (!team.IsActive)
		{
			_logger.LogDebug("Team {TeamId} is not active", teamId);
			return false;
		}

		return true;
	}
}
