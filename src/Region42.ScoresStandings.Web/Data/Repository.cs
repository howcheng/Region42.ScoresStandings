using System.Linq.Expressions;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Storage;

namespace Region42.ScoresStandings.Web.Data;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
	private readonly CompetitionDataContext _context;

	public Repository(CompetitionDataContext context)
	{
		_context = context;
	}

	public async Task<T?> GetByIdAsync(int id)
	{
		await EnsureLoadedAsync();
		return GetQueryable().FirstOrDefault(e => e.Id == id);
	}

	public async Task<IEnumerable<T>> GetAllAsync()
	{
		await EnsureLoadedAsync();
		return GetQueryable().ToList();
	}

	public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
	{
		await EnsureLoadedAsync();
		return GetQueryable().Where(predicate.Compile()).ToList();
	}

	public async Task AddAsync(T entity)
	{
		await EnsureLoadedAsync();
		_context.Add(entity);
		await Task.CompletedTask;
	}

	public void Update(T entity)
	{
		_context.Update(entity);
	}

	public void Delete(T entity)
	{
		_context.Delete(entity);
	}

	public Task<int> SaveChangesAsync()
	{
		return _context.SaveChangesAsync();
	}

	private async Task EnsureLoadedAsync()
	{
		await _context.LoadAllAsync();
	}

	private IQueryable<T> GetQueryable()
	{
		if (typeof(T) == typeof(Season))
		{
			return (IQueryable<T>)_context.Seasons.AsQueryable();
		}

		if (typeof(T) == typeof(Division))
		{
			return (IQueryable<T>)_context.Divisions.AsQueryable();
		}

		if (typeof(T) == typeof(Team))
		{
			return (IQueryable<T>)WireTeams(_context.Teams).AsQueryable();
		}

		if (typeof(T) == typeof(Game))
		{
			return (IQueryable<T>)WireGames(_context.Games).AsQueryable();
		}

		if (typeof(T) == typeof(Score))
		{
			return (IQueryable<T>)WireScores(_context.Scores).AsQueryable();
		}

		if (typeof(T) == typeof(VolunteerPoints))
		{
			return (IQueryable<T>)WireVolunteerPoints(_context.VolunteerPoints).AsQueryable();
		}

		if (typeof(T) == typeof(Settings))
		{
			return (IQueryable<T>)new[] { _context.Settings }.AsQueryable();
		}

		if (typeof(T) == typeof(User))
		{
			return Enumerable.Empty<T>().AsQueryable();
		}

		throw new NotSupportedException($"Repository for type {typeof(T).Name} is not supported.");
	}

	private IEnumerable<Team> WireTeams(IReadOnlyList<Team> teams)
	{
		var divisions = _context.Divisions.ToDictionary(d => d.Id);
		foreach (var team in teams)
		{
			if (divisions.TryGetValue(team.DivisionId, out var division))
			{
				team.Division = division;
			}
		}

		return teams;
	}

	private IEnumerable<Game> WireGames(IReadOnlyList<Game> games)
	{
		var teams = _context.Teams.ToDictionary(t => t.Id);
		var divisions = _context.Divisions.ToDictionary(d => d.Id);

		foreach (var game in games)
		{
			if (teams.TryGetValue(game.HomeTeamId, out var homeTeam))
			{
				game.HomeTeam = homeTeam;
			}

			if (teams.TryGetValue(game.AwayTeamId, out var awayTeam))
			{
				game.AwayTeam = awayTeam;
			}

			if (divisions.TryGetValue(game.DivisionId, out var division))
			{
				game.Division = division;
			}

			game.Score ??= _context.Scores.FirstOrDefault(s => s.GameId == game.Id);
			if (game.Score != null)
			{
				game.Score.Game = game;
			}
		}

		return games;
	}

	private IEnumerable<Score> WireScores(IReadOnlyList<Score> scores)
	{
		var games = WireGames(_context.Games).ToDictionary(g => g.Id);
		foreach (var score in scores)
		{
			if (games.TryGetValue(score.GameId, out var game))
			{
				score.Game = game;
				game.Score = score;
			}
		}

		return scores;
	}

	private IEnumerable<VolunteerPoints> WireVolunteerPoints(IReadOnlyList<VolunteerPoints> volunteerPoints)
	{
		var teams = WireTeams(_context.Teams).ToDictionary(t => t.Id);
		foreach (var vp in volunteerPoints)
		{
			if (teams.TryGetValue(vp.TeamId, out var team))
			{
				vp.Team = team;
			}
		}

		return volunteerPoints;
	}
}
