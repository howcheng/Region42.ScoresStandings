using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Infrastructure.Repositories;
using Region42.ScoresStandings.Infrastructure.Storage;

namespace Region42.ScoresStandings.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
		var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

		if (string.Equals(storageOptions.Provider, "Gcs", StringComparison.OrdinalIgnoreCase))
		{
			services.AddSingleton(StorageClient.Create());
			services.AddSingleton<ICompetitionDataStore, GcsCompetitionDataStore>();
		}
		else
		{
			services.AddSingleton<ICompetitionDataStore, LocalFileCompetitionDataStore>();
		}

		services.AddScoped<ICompetitionDataContext, CompetitionDataContext>();
		services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

		return services;
	}
}
