using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.HttpOverrides;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Authorization;
using Region42.ScoresStandings.Web.Data;
using Region42.ScoresStandings.Web.Middleware;
using Region42.ScoresStandings.Web.Storage;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

var mvcBuilder = builder.Services.AddControllersWithViews();

// Configure forwarded headers to handle HTTPS redirection on Google Cloud Run hosting
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownNetworks.Clear();
	options.KnownProxies.Clear();
});

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
	options.Cookie.IsEssential = true;
});

// Configure TempData to use session storage instead of cookies
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider,
	Microsoft.AspNetCore.Mvc.ViewFeatures.SessionStateTempDataProvider>();

builder.Services.AddHttpContextAccessor();

// Configure HSTS to use OWASP recommended values (1 year, subdomains, and preload)
builder.Services.AddHsts(options =>
{
	options.Preload = true;
	options.IncludeSubDomains = true;
	options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
var storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

if (string.Equals(storageOptions.Provider, "Gcs", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddSingleton(StorageClient.Create());
	builder.Services.AddSingleton<ICompetitionDataStore, GcsCompetitionDataStore>();
}
else
{
	builder.Services.AddSingleton<ICompetitionDataStore, LocalFileCompetitionDataStore>();
}

builder.Services.AddScoped<CompetitionDataContext>();
builder.Services.AddScoped<ICompetitionDataContext>(sp => sp.GetRequiredService<CompetitionDataContext>());
builder.Services.AddScoped<IRegion42DbContext>(sp => sp.GetRequiredService<CompetitionDataContext>());
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<IVolunteerPointsService, VolunteerPointsService>();
builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<IStandingsRefreshService, StandingsRefreshService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddScoped<PostgresToJsonExporter>();

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
	options.Scope.Add("email");
	options.SaveTokens = true;
});

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("AdminPolicy", policy =>
	{
		policy.RequireAuthenticatedUser();
		policy.Requirements.Add(new DomainRequirement("aysoregion42.org"));
	});

builder.Services.AddSingleton<IAuthorizationHandler, DomainRequirementHandler>();

var app = builder.Build();

if (args.Contains("--export-from-postgres", StringComparer.OrdinalIgnoreCase))
{
	using var scope = app.Services.CreateScope();
	var exporter = scope.ServiceProvider.GetRequiredService<PostgresToJsonExporter>();
	await exporter.ExportAsync();
	return;
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseRouting();
app.UseThemeCookie();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();
