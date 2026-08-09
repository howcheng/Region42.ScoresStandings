# IRegion42DbContext Interface

## Purpose
The `IRegion42DbContext` interface provides an abstraction layer over Entity Framework Core's `DbContext`, making it easier to unit test services that depend on database access.

## Why We Created This Interface

1. **Testability**: Enables mocking the database context in unit tests without requiring a real database connection
2. **Dependency Inversion**: Services depend on an abstraction (interface) rather than a concrete implementation
3. **Framework Independence**: The Domain layer doesn't need to reference Entity Framework packages

## Interface Definition

Located in: `Region42.ScoresStandings.Domain/Interfaces/IRegion42DbContext.cs`

```csharp
public interface IRegion42DbContext
{
	Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

	// Methods to query entity sets
	IQueryable<Season> GetSeasons();
	IQueryable<Division> GetDivisions();
	IQueryable<Team> GetTeams();
	IQueryable<Game> GetGames();
	IQueryable<Score> GetScores();
	IQueryable<VolunteerPoints> GetVolunteerPoints();
	IQueryable<User> GetUsers();

	// Generic set access for repository pattern
	IQueryable<T> Set<T>() where T : BaseEntity;

	// Methods for tracking entity state
	void Add<T>(T entity) where T : BaseEntity;
	void Update<T>(T entity) where T : BaseEntity;
	void Remove<T>(T entity) where T : BaseEntity;
}
```

## Implementation

The `Region42DbContext` class implements this interface by delegating to Entity Framework Core's DbContext methods:

```csharp
public class Region42DbContext : DbContext, IRegion42DbContext
{
	// DbSet properties for direct EF access
	public DbSet<Season> Seasons => Set<Season>();
	// ... other DbSets

	// Interface implementation
	public IQueryable<Season> GetSeasons() => Seasons;
	IQueryable<T> IRegion42DbContext.Set<T>() => Set<T>();
	void IRegion42DbContext.Add<T>(T entity) => Add(entity);
	void IRegion42DbContext.Update<T>(T entity) => Update(entity);
	void IRegion42DbContext.Remove<T>(T entity) => Remove(entity);
}
```

## Usage in Services

Services should depend on `IRegion42DbContext` rather than the concrete `Region42DbContext`:

```csharp
public class TeamService : ITeamService
{
	private readonly IRegion42DbContext _context;

	public TeamService(IRegion42DbContext context)
	{
		_context = context;
	}

	public async Task<IEnumerable<Team>> GetTeamsByDivisionAsync(int divisionId)
	{
		return await _context.GetTeams()
			.Where(t => t.DivisionId == divisionId)
			.ToListAsync();
	}
}
```

## Unit Testing with Mocks

Using a mocking framework like Moq, you can create test doubles:

```csharp
[Fact]
public async Task GetTeamsByDivision_ReturnsCorrectTeams()
{
	// Arrange
	var mockContext = new Mock<IRegion42DbContext>();
	var teams = new List<Team>
	{
		new Team { Id = 1, Name = "Team A", DivisionId = 1 },
		new Team { Id = 2, Name = "Team B", DivisionId = 1 },
		new Team { Id = 3, Name = "Team C", DivisionId = 2 }
	}.AsQueryable();

	mockContext.Setup(x => x.GetTeams()).Returns(teams);

	var service = new TeamService(mockContext.Object);

	// Act
	var result = await service.GetTeamsByDivisionAsync(1);

	// Assert
	Assert.Equal(2, result.Count());
}
```

## In-Memory Database Testing

For integration tests, you can still use the concrete `Region42DbContext` with an in-memory database:

```csharp
[Fact]
public async Task GetTeamsByDivision_IntegrationTest()
{
	// Arrange
	var options = new DbContextOptionsBuilder<Region42DbContext>()
		.UseInMemoryDatabase(databaseName: "TestDb")
		.Options;

	var httpContextAccessor = new Mock<IHttpContextAccessor>();
	IRegion42DbContext context = new Region42DbContext(options, httpContextAccessor.Object);

	// Add test data
	context.Add(new Team { Id = 1, Name = "Team A", DivisionId = 1 });
	await context.SaveChangesAsync();

	var service = new TeamService(context);

	// Act
	var result = await service.GetTeamsByDivisionAsync(1);

	// Assert
	Assert.Single(result);
}
```

## Dependency Injection Setup

In `Program.cs`, register both the interface and implementation:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<Region42DbContext>(options =>
	options.UseNpgsql(connectionString));
builder.Services.AddScoped<IRegion42DbContext>(provider => 
	provider.GetRequiredService<Region42DbContext>());
```

## Benefits

1. **Easier to Test**: Services can be tested in isolation without database dependencies
2. **Faster Tests**: Unit tests run quickly without database I/O
3. **Cleaner Architecture**: Domain layer stays infrastructure-agnostic
4. **Flexible Mocking**: Can simulate database errors, concurrency issues, etc. in tests
5. **Better Design**: Forces thinking about what operations services actually need from the database

## Trade-offs

1. **Additional Abstraction**: Extra layer of indirection
2. **Manual Maintenance**: Interface must be kept in sync with DbContext needs
3. **Query Complexity**: Some advanced EF features may be harder to abstract

For this application, the testability benefits outweigh the minor additional complexity.
