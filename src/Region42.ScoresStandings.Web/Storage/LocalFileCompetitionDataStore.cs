using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Helpers;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Storage;

public class LocalFileCompetitionDataStore : ICompetitionDataStore
{
	private const string IndexPath = "index.json";
	private const string GenerationSuffix = ".generation";

	private readonly string _rootPath;

	public LocalFileCompetitionDataStore(IOptions<StorageOptions> options)
	{
		_rootPath = Path.GetFullPath(options.Value.LocalRoot);
		Directory.CreateDirectory(_rootPath);
	}

	public Task<StorageDocument<IndexDocument>> GetIndexAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(ReadDocument<IndexDocument>(IndexPath));
	}

	public Task SaveIndexAsync(IndexDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		WriteDocument(IndexPath, document, expectedGeneration);
		return Task.CompletedTask;
	}

	public Task<StorageDocument<SeasonDocument>> GetSeasonDocumentAsync(int year, string competitionSlug, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetSeasonObjectPath(year, competitionSlug);
		return Task.FromResult(ReadDocument<SeasonDocument>(path));
	}

	public Task SaveSeasonDocumentAsync(int year, string competitionSlug, SeasonDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetSeasonObjectPath(year, competitionSlug);
		WriteDocument(path, document, expectedGeneration);
		return Task.CompletedTask;
	}

	public Task<StorageDocument<DivisionDataDocument>> GetDivisionDataAsync(int year, string competitionSlug, string divisionKey, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetDivisionObjectPath(year, competitionSlug, divisionKey);
		return Task.FromResult(ReadDocument<DivisionDataDocument>(path));
	}

	public Task SaveDivisionDataAsync(int year, string competitionSlug, string divisionKey, DivisionDataDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetDivisionObjectPath(year, competitionSlug, divisionKey);
		WriteDocument(path, document, expectedGeneration);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<string>> ListCompetitionPathsAsync(CancellationToken cancellationToken = default)
	{
		var paths = new List<string>();

		if (!Directory.Exists(_rootPath))
		{
			return Task.FromResult<IReadOnlyList<string>>(paths);
		}

		foreach (var yearDir in Directory.GetDirectories(_rootPath))
		{
			var yearName = Path.GetFileName(yearDir);
			if (!int.TryParse(yearName, out _))
			{
				continue;
			}

			foreach (var competitionDir in Directory.GetDirectories(yearDir))
			{
				var seasonFile = Path.Combine(competitionDir, "season.json");
				if (File.Exists(seasonFile))
				{
					paths.Add($"{yearName}/{Path.GetFileName(competitionDir)}");
				}
			}
		}

		return Task.FromResult<IReadOnlyList<string>>(paths);
	}

	private StorageDocument<T> ReadDocument<T>(string objectPath)
	{
		var fullPath = GetFullPath(objectPath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"Storage object not found: {objectPath}", fullPath);
		}

		var json = File.ReadAllText(fullPath);
		var generation = ReadGeneration(objectPath);

		return new StorageDocument<T>
		{
			Content = CompetitionJsonSerializer.Deserialize<T>(json),
			Generation = generation,
			ObjectPath = objectPath
		};
	}

	private void WriteDocument<T>(string objectPath, T document, long? expectedGeneration)
	{
		var fullPath = GetFullPath(objectPath);
		var generationPath = GetGenerationPath(objectPath);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

		var currentGeneration = ReadGeneration(objectPath);
		if (expectedGeneration.HasValue && currentGeneration != expectedGeneration.Value)
		{
			throw new StorageConcurrencyException(objectPath);
		}

		var nextGeneration = (currentGeneration ?? 0) + 1;
		var tempPath = fullPath + ".tmp";

		File.WriteAllText(tempPath, CompetitionJsonSerializer.Serialize(document));
		File.Move(tempPath, fullPath, overwrite: true);
		File.WriteAllText(generationPath, nextGeneration.ToString());
	}

	private long? ReadGeneration(string objectPath)
	{
		var generationPath = GetGenerationPath(objectPath);
		if (!File.Exists(generationPath))
		{
			return File.Exists(GetFullPath(objectPath)) ? 1 : null;
		}

		return long.Parse(File.ReadAllText(generationPath));
	}

	private string GetFullPath(string objectPath)
	{
		return Path.Combine(_rootPath, objectPath.Replace('/', Path.DirectorySeparatorChar));
	}

	private string GetGenerationPath(string objectPath)
	{
		return GetFullPath(objectPath + GenerationSuffix);
	}
}
