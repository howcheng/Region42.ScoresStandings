using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Domain.Interfaces;

public interface ICompetitionDataContext
{
	Task LoadAllAsync(CancellationToken cancellationToken = default);
	Task LoadSeasonAsync(int year, string competitionSlug, CancellationToken cancellationToken = default);
	Task LoadDivisionDataAsync(int divisionId, CancellationToken cancellationToken = default);

	IndexDocument Index { get; }
	IReadOnlyList<Season> Seasons { get; }
	IReadOnlyList<Division> Divisions { get; }
	IReadOnlyList<Team> Teams { get; }
	IReadOnlyList<Game> Games { get; }
	IReadOnlyList<Score> Scores { get; }
	IReadOnlyList<VolunteerPoints> VolunteerPoints { get; }
	Settings Settings { get; }

	Dictionary<string, StandingsSnapshotDocument> GetStandingsByRound(int divisionId);

	void Add<T>(T entity) where T : BaseEntity;
	void Update<T>(T entity) where T : BaseEntity;
	void Delete<T>(T entity) where T : BaseEntity;

	Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
	Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

	void SetStandingsForDivision(int divisionId, Dictionary<string, StandingsSnapshotDocument> standingsByRound);
	(int Year, string Slug)? GetCompetitionPathForSeason(int seasonId);
	string? GetDivisionKey(int divisionId);
	int AllocateNextId();
}
