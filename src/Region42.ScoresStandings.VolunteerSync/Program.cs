using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Region42.ScoresStandings.VolunteerSync;
using Region42.ScoresStandings.VolunteerSync.Configuration;
using Region42.ScoresStandings.VolunteerSync.Interfaces;
using Region42.ScoresStandings.VolunteerSync.Services;

var builder = Host.CreateApplicationBuilder(args);

// Console/worker host does not load user secrets automatically (unlike WebApplication.CreateBuilder).
if (builder.Environment.IsDevelopment())
{
	builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Services.Configure<VolunteerSyncOptions>(
	builder.Configuration.GetSection(VolunteerSyncOptions.SectionName));
builder.Services.PostConfigure<VolunteerSyncOptions>(options =>
{
	options.SourceUsername = options.SourceUsername.Trim();
	options.SourcePassword = options.SourcePassword.Trim();
});

builder.Services.AddHttpClient();
builder.Services.AddHttpClient(nameof(ScoresStandingsApiClient));
builder.Services.AddHttpClient("GoogleMetadata");

builder.Services.AddSingleton<IVolunteerPointsSourceClient, VolunteerPointsSourceClient>();
builder.Services.AddSingleton<IVolunteerPointsFileParser, VolunteerPointsExcelParser>();
builder.Services.AddSingleton<VolunteerPointsImportPreparer>();
builder.Services.AddSingleton<IScoresStandingsApiClient, ScoresStandingsApiClient>();
builder.Services.AddHostedService<VolunteerSyncWorker>();

await builder.Build().RunAsync();
