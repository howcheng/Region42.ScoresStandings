using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Data;

public class Region42DbContext : DbContext, IRegion42DbContext
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public Region42DbContext(DbContextOptions<Region42DbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public DbSet<Season> Seasons => Set<Season>();
	public DbSet<Division> Divisions => Set<Division>();
	public DbSet<Team> Teams => Set<Team>();
	public DbSet<Game> Games => Set<Game>();
	public DbSet<Score> Scores => Set<Score>();
	public DbSet<VolunteerPoints> VolunteerPoints => Set<VolunteerPoints>();
	public DbSet<User> Users => Set<User>();
	public DbSet<Settings> Settings => Set<Settings>();

	// IRegion42DbContext implementation
	public IQueryable<Season> GetSeasons() => Seasons;
	public IQueryable<Division> GetDivisions() => Divisions;
	public IQueryable<Team> GetTeams() => Teams;
	public IQueryable<Game> GetGames() => Games;
	public IQueryable<Score> GetScores() => Scores;
	public IQueryable<VolunteerPoints> GetVolunteerPoints() => VolunteerPoints;
	public IQueryable<User> GetUsers() => Users;
	public IQueryable<Settings> GetSettings() => Settings;

	IQueryable<T> IRegion42DbContext.Set<T>() => Set<T>();

	void IRegion42DbContext.Add<T>(T entity) => Add(entity);
	void IRegion42DbContext.Update<T>(T entity) => Update(entity);
	void IRegion42DbContext.Remove<T>(T entity) => Remove(entity);

	// Transaction support
	public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
	{
		var efTransaction = await Database.BeginTransactionAsync(cancellationToken);
		return new DbTransactionWrapper(efTransaction);
	}

	public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
		var now = DateTime.UtcNow;

		foreach (var entry in ChangeTracker.Entries<BaseEntity>())
		{
			switch (entry.State)
			{
				case EntityState.Added:
					entry.Entity.CreatedAt = now;
					entry.Entity.CreatedBy = currentUser;
					entry.Entity.ModifiedAt = now;
					entry.Entity.ModifiedBy = currentUser;
					entry.Entity.RowVersion = 1;
					break;

				case EntityState.Modified:
					entry.Entity.ModifiedAt = now;
					entry.Entity.ModifiedBy = currentUser;
					entry.Entity.RowVersion++;
					break;
			}
		}

		return await base.SaveChangesAsync(cancellationToken);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Season configuration
		modelBuilder.Entity<Season>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Year).IsRequired();
			entity.Property(e => e.IsActive).IsRequired();
			entity.Property(e => e.CustomMessage).HasMaxLength(500);
			entity.Property(e => e.RowVersion).IsConcurrencyToken();
			entity.HasIndex(e => e.Year);
			// StartDate is a computed property (August 1 of Year) - not mapped to database
			entity.Ignore(e => e.StartDate);
		});

		// Division configuration
		modelBuilder.Entity<Division>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.AgeGroup).IsRequired();
			entity.Property(e => e.Gender).IsRequired();
			entity.Property(e => e.TotalRounds).IsRequired();
			entity.Property(e => e.PlayoffSpots).IsRequired().HasDefaultValue(1);
			entity.Property(e => e.ScrimmageRounds).IsRequired().HasDefaultValue(0);
			entity.Property(e => e.CustomMessage).HasMaxLength(500);
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasOne(e => e.Season)
				.WithMany(s => s.Divisions)
				.HasForeignKey(e => e.SeasonId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.SeasonId, e.AgeGroup, e.Gender }).IsUnique();
		});

		// Team configuration
		modelBuilder.Entity<Team>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.ShortName).IsRequired().HasMaxLength(50);
			entity.Property(e => e.ContactName).HasMaxLength(200);
			entity.Property(e => e.ContactEmail).HasMaxLength(200);
			entity.Property(e => e.ContactPhone).HasMaxLength(50);
			entity.Property(e => e.IsActive).IsRequired();
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasOne(e => e.Division)
				.WithMany(d => d.Teams)
				.HasForeignKey(e => e.DivisionId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.DivisionId, e.Name });
		});

		// Game configuration
		modelBuilder.Entity<Game>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.ScheduledDateTime).IsRequired();
			entity.Property(e => e.Round).IsRequired();
			entity.Property(e => e.Location).HasMaxLength(200);
			entity.Property(e => e.Status).IsRequired();
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasOne(e => e.Division)
				.WithMany(d => d.Games)
				.HasForeignKey(e => e.DivisionId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasOne(e => e.HomeTeam)
				.WithMany(t => t.HomeGames)
				.HasForeignKey(e => e.HomeTeamId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasOne(e => e.AwayTeam)
				.WithMany(t => t.AwayGames)
				.HasForeignKey(e => e.AwayTeamId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.DivisionId, e.Round, e.ScheduledDateTime });
			entity.HasIndex(e => e.ScheduledDateTime);
		});

		// Score configuration
		modelBuilder.Entity<Score>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.GameId).IsRequired();
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasOne(e => e.Game)
				.WithOne(g => g.Score)
				.HasForeignKey<Score>(e => e.GameId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(e => e.GameId).IsUnique();
		});

		// VolunteerPoints configuration
		modelBuilder.Entity<VolunteerPoints>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Round).IsRequired();
			entity.Property(e => e.Points).IsRequired();
			entity.Property(e => e.Notes).HasMaxLength(500);
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasOne(e => e.Team)
				.WithMany(t => t.VolunteerPoints)
				.HasForeignKey(e => e.TeamId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(e => new { e.TeamId, e.Round });
		});

		// User configuration
		modelBuilder.Entity<User>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.GoogleId).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
			entity.Property(e => e.DisplayName).HasMaxLength(200);
			entity.Property(e => e.LastLogin).IsRequired();
			entity.Property(e => e.RowVersion).IsConcurrencyToken();

			entity.HasIndex(e => e.GoogleId).IsUnique();
			entity.HasIndex(e => e.Email);
		});

		// Settings configuration (singleton pattern - only one record)
		modelBuilder.Entity<Settings>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.MinVolunteerPointsForPlayoff).IsRequired().HasDefaultValue(0);
			entity.Property(e => e.DefaultPlayoffSpots).IsRequired().HasDefaultValue(1);
			entity.Property(e => e.RowVersion).IsConcurrencyToken();
		});

		// Audit fields configuration for BaseEntity
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
			{
				modelBuilder.Entity(entityType.ClrType)
					.Property<DateTime>("CreatedAt")
					.IsRequired();

				modelBuilder.Entity(entityType.ClrType)
					.Property<DateTime>("ModifiedAt")
					.IsRequired();

				modelBuilder.Entity(entityType.ClrType)
					.Property<string>("CreatedBy")
					.IsRequired()
					.HasMaxLength(200);

				modelBuilder.Entity(entityType.ClrType)
					.Property<string>("ModifiedBy")
					.IsRequired()
					.HasMaxLength(200);
			}
		}
	}
}
