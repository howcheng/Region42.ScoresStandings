using Microsoft.EntityFrameworkCore;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Data;
using Region42.ScoresStandings.Web.Authorization;
using Region42.ScoresStandings.Web.Middleware;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews();

// Enable runtime compilation in Development environment for faster development
if (builder.Environment.IsDevelopment())
{
	mvcBuilder.AddRazorRuntimeCompilation();
}

// Configure session and TempData to use session storage instead of cookies
// This prevents 431 errors when CSV preview data exceeds cookie size limits
builder.Services.AddDistributedMemoryCache(); // Use in-memory cache for session storage
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(30);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true; // Make session cookie essential for GDPR
});

// Configure TempData to use session storage instead of cookies
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider,
	Microsoft.AspNetCore.Mvc.ViewFeatures.SessionStateTempDataProvider>();

// Register IHttpContextAccessor for audit tracking
builder.Services.AddHttpContextAccessor();

// Register DbContext with connection string from configuration/user secrets
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<Region42DbContext>(options =>
	options.UseNpgsql(connectionString));

// Register IRegion42DbContext interface for dependency injection
builder.Services.AddScoped<IRegion42DbContext>(provider =>
	provider.GetRequiredService<Region42DbContext>());

// Register generic repository using open generics
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register application services
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<IVolunteerPointsService, VolunteerPointsService>();
builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();

// Configure Google OAuth Authentication
// Security model: Two-layer approach
// 1. Domain restriction: Configure in Google Cloud Console OAuth consent screen
//    to only allow @aysoregion42.org
// 2. User whitelist: Check authenticated user against User table (future implementation)
//    See plan documentation for details
builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = "Cookies";
	options.DefaultChallengeScheme = "Google";
})
.AddCookie("Cookies")
.AddGoogle("Google", options =>
{
	options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
		?? throw new InvalidOperationException("Google ClientId not found in configuration.");
	options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
		?? throw new InvalidOperationException("Google ClientSecret not found in configuration.");

	// Request email scope to get user's email address
	options.Scope.Add("email");
	options.SaveTokens = true;
});

// Add authorization with domain-restricted AdminPolicy
builder.Services.AddAuthorizationBuilder()
	.AddPolicy("AdminPolicy", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.Requirements.Add(new DomainRequirement("aysoregion42.org"));
	});

builder.Services.AddSingleton<IAuthorizationHandler, DomainRequirementHandler>();

var app = builder.Build();

// Apply pending migrations automatically in Development environment only
// Production migrations should be applied via deployment pipeline
if (app.Environment.IsDevelopment())
{
	using (var scope = app.Services.CreateScope())
	{
		var dbContext = scope.ServiceProvider.GetRequiredService<Region42DbContext>();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

		try
		{
			var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
			if (pendingMigrations.Any())
			{
				logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
					pendingMigrations.Count(),
					string.Join(", ", pendingMigrations));

				await dbContext.Database.MigrateAsync();

				logger.LogInformation("Database migrations applied successfully");
			}
			else
			{
				logger.LogInformation("Database is up to date, no pending migrations");
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "An error occurred while migrating the database");
			throw; // Fail fast in development
		}
	}
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();

// Add Content Security Policy headers
app.UseContentSecurityPolicy();

app.UseRouting();

// Enable session middleware - MUST come before UseAuthentication/UseAuthorization
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();


app.Run();
