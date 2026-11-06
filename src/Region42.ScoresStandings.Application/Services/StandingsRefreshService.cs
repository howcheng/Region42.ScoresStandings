using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

public class StandingsRefreshService : IStandingsRefreshService
{
	private readonly ICompetitionDataContext _context;
	private readonly ITournamentService? _tournamentService;

	public StandingsRefreshService(
		ICompetitionDataContext context,
		ITournamentService? tournamentService = null)
	{
		_context = context;
		_tournamentService = tournamentService;
	}

	public void RefreshDivisionStandings(int divisionId)
	{
		RefreshDivisionStandingsAsync(divisionId).GetAwaiter().GetResult();
	}

	public async Task RefreshDivisionStandingsAsync(int divisionId)
	{
		await _context.LoadAllAsync();

		var division = _context.Divisions.FirstOrDefault(d => d.Id == divisionId)
			?? throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));

		if (_tournamentService != null && division.EndOfSeasonTournamentFormat != Domain.Enums.TournamentFormatType.None)
		{
			try
			{
				await _tournamentService.EnsureTournamentGamesAsync(divisionId, saveChanges: false);
			}
			catch (NotSupportedException)
			{
				// Ignore when division team count does not have a registered bracket/pool builder yet
			}
		}

		var teams = _context.Teams.Where(t => t.DivisionId == divisionId).ToList();
		var games = _context.Games.Where(g => g.DivisionId == divisionId).ToList();
		var gameIds = games.Select(g => g.Id).ToHashSet();
		var scores = _context.Scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = teams.Select(t => t.Id).ToHashSet();
		var volunteerPoints = _context.VolunteerPoints.Where(vp => teamIds.Contains(vp.TeamId)).ToList();
		var minVolunteerPoints = _context.Settings.MinVolunteerPointsForPlayoff;
		var maxVolunteerPoints = _context.Settings.MaxVolunteerPointsPerSeason;

		var standingsByRound = StandingsCalculator.CalculateAllRounds(
			division,
			teams,
			games,
			scores,
			volunteerPoints,
			minVolunteerPoints,
			maxVolunteerPoints);

		_context.SetStandingsForDivision(divisionId, standingsByRound);
	}
}
