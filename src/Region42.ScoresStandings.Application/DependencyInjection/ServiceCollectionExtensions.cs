﻿using Microsoft.Extensions.DependencyInjection;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services;

namespace Region42.ScoresStandings.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddScoped<ISeasonService, SeasonService>();
		services.AddScoped<ITeamService, TeamService>();
		services.AddScoped<IGameService, GameService>();
		services.AddScoped<IScoreService, ScoreService>();
		services.AddScoped<IVolunteerPointsService, VolunteerPointsService>();
		services.AddScoped<IStandingsService, StandingsService>();
		services.AddScoped<IStandingsRefreshService, StandingsRefreshService>();
		services.AddScoped<ICsvImportService, CsvImportService>();

		return services;
	}
}
