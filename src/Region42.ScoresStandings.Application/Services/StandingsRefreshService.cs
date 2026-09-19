using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

public class StandingsRefreshService : IStandingsRefreshService
{
	private readonly ICompetitionDataContext _context;

	public StandingsRefreshService(ICompetitionDataContext context)
	{
		_context = context;
	}

	public async Task RefreshDivisionStandingsAsync(int divisionId)
	{
		await _context.LoadAllAsync();

		var division = _context.Divisions.FirstOrDefault(d => d.Id == divisionId)
			?? throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));

		var teams = _context.Teams.Where(t => t.DivisionId == divisionId).ToList();
		var games = _context.Games.Where(g => g.DivisionId == divisionId).ToList();
		var gameIds = games.Select(g => g.Id).ToHashSet();
		var scores = _context.Scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = teams.Select(t => t.Id).ToHashSet();
		var volunteerPoints = _context.VolunteerPoints.Where(vp => teamIds.Contains(vp.TeamId)).ToList();
		var minVolunteerPoints = _context.Settings.MinVolunteerPointsForPlayoff;

		var standingsByRound = StandingsCalculator.CalculateAllRounds(
			division,
			teams,
			games,
			scores,
			volunteerPoints,
			minVolunteerPoints);

		_context.SetStandingsForDivision(divisionId, standingsByRound);
	}
}
