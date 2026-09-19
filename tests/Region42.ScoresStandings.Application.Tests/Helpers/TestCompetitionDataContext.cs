using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

public class TestCompetitionDataContext : ICompetitionDataContext
{
	private readonly Dictionary<int, Dictionary<string, StandingsSnapshotDocument>> _standings = new();

	public List<Season> Seasons { get; } = new();
	public List<Division> Divisions { get; } = new();
	public List<Team> Teams { get; } = new();
	public List<Game> Games { get; } = new();
	public List<Score> Scores { get; } = new();
	public List<VolunteerPoints> VolunteerPoints { get; } = new();
	public Settings Settings { get; set; } = new() { MinVolunteerPointsForPlayoff = 0, DefaultPlayoffSpots = 1 };
	public IndexDocument Index { get; } = new();

	IReadOnlyList<Season> ICompetitionDataContext.Seasons => Seasons;
	IReadOnlyList<Division> ICompetitionDataContext.Divisions => Divisions;
	IReadOnlyList<Team> ICompetitionDataContext.Teams => Teams;
	IReadOnlyList<Game> ICompetitionDataContext.Games => Games;
	IReadOnlyList<Score> ICompetitionDataContext.Scores => Scores;
	IReadOnlyList<VolunteerPoints> ICompetitionDataContext.VolunteerPoints => VolunteerPoints;

	public Task LoadAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	public Task LoadSeasonAsync(int year, string competitionSlug, CancellationToken cancellationToken = default) => Task.CompletedTask;
	public Task LoadDivisionDataAsync(int divisionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Dictionary<string, StandingsSnapshotDocument> GetStandingsByRound(int divisionId)
	{
		return _standings.TryGetValue(divisionId, out var value)
			? value
			: new Dictionary<string, StandingsSnapshotDocument>();
	}

	public void SetStandingsForDivision(int divisionId, Dictionary<string, StandingsSnapshotDocument> standingsByRound)
	{
		_standings[divisionId] = standingsByRound;
	}

	public (int Year, string Slug)? GetCompetitionPathForSeason(int seasonId) => (2026, "core-season");
	public string? GetDivisionKey(int divisionId) => "10u-boys";

	public void Add<T>(T entity) where T : BaseEntity
	{
		switch (entity)
		{
			case Season season: Seasons.Add(season); break;
			case Division division: Divisions.Add(division); break;
			case Team team: Teams.Add(team); break;
			case Game game: Games.Add(game); break;
			case Score score: Scores.Add(score); break;
			case VolunteerPoints vp: VolunteerPoints.Add(vp); break;
			case Settings settings: Settings = settings; break;
		}
	}

	public void Update<T>(T entity) where T : BaseEntity
	{
		Delete(entity);
		Add(entity);
	}

	public void Delete<T>(T entity) where T : BaseEntity
	{
		switch (entity)
		{
			case Season season: Seasons.RemoveAll(s => s.Id == season.Id); break;
			case Division division: Divisions.RemoveAll(d => d.Id == division.Id); break;
			case Team team: Teams.RemoveAll(t => t.Id == team.Id); break;
			case Game game: Games.RemoveAll(g => g.Id == game.Id); break;
			case Score score: Scores.RemoveAll(s => s.Id == score.Id); break;
			case VolunteerPoints vp: VolunteerPoints.RemoveAll(v => v.Id == vp.Id); break;
		}
	}

	public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

	public Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IDbTransaction>(new TestDbTransaction());
	}

	public int AllocateNextId() => (Seasons.Concat<BaseEntity>(Divisions).Concat(Teams).Concat(Games).Concat(Scores).Concat(VolunteerPoints).Select(e => e.Id).DefaultIfEmpty(0).Max()) + 1;

	private sealed class TestDbTransaction : IDbTransaction
	{
		public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
		public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
