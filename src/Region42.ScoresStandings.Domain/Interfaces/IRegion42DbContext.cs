using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Domain.Interfaces;

public interface IRegion42DbContext
{
	Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

	// Methods to expose entity sets without exposing EF-specific types
	IQueryable<Season> GetSeasons();
	IQueryable<Division> GetDivisions();
	IQueryable<Team> GetTeams();
	IQueryable<Game> GetGames();
	IQueryable<Score> GetScores();
	IQueryable<VolunteerPoints> GetVolunteerPoints();
	IQueryable<User> GetUsers();
	IQueryable<Settings> GetSettings();

	// Generic set access for repository pattern
	IQueryable<T> Set<T>() where T : BaseEntity;

	// Methods for tracking entities
	void Add<T>(T entity) where T : BaseEntity;
	void Update<T>(T entity) where T : BaseEntity;
	void Remove<T>(T entity) where T : BaseEntity;

	// Transaction support
	Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
