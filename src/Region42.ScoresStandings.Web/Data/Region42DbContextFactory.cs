using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Region42.ScoresStandings.Web.Data;

public class Region42DbContextFactory : IDesignTimeDbContextFactory<Region42DbContext>
{
	public Region42DbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<Region42DbContext>();

		// For design-time, we'll use a placeholder connection string
		// The actual connection string comes from user secrets at runtime
		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: false)
			.AddUserSecrets<Program>()
			.Build();

		var connectionString = configuration.GetConnectionString("DefaultConnection");

		optionsBuilder.UseNpgsql(connectionString);

		// Create a mock IHttpContextAccessor for design-time
		var httpContextAccessor = new HttpContextAccessor();

		return new Region42DbContext(optionsBuilder.Options, httpContextAccessor);
	}
}
