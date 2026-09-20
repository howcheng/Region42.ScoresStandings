using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Helpers;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Infrastructure.Storage;

public class GcsCompetitionDataStore : ICompetitionDataStore
{
	private const string IndexPath = "index.json";
	private const int MaxRetryAttempts = 3;

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;

	public GcsCompetitionDataStore(StorageClient storageClient, IOptions<StorageOptions> options)
	{
		_storageClient = storageClient;
		_bucketName = options.Value.BucketName;
	}

	public async Task<StorageDocument<IndexDocument>> GetIndexAsync(CancellationToken cancellationToken = default)
	{
		return await GetObjectAsync<IndexDocument>(IndexPath, cancellationToken);
	}

	public Task SaveIndexAsync(IndexDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		return SaveObjectAsync(IndexPath, document, expectedGeneration, cancellationToken);
	}

	public Task<StorageDocument<SeasonDocument>> GetSeasonDocumentAsync(int year, string competitionSlug, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetSeasonObjectPath(year, competitionSlug);
		return GetObjectAsync<SeasonDocument>(path, cancellationToken);
	}

	public Task SaveSeasonDocumentAsync(int year, string competitionSlug, SeasonDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetSeasonObjectPath(year, competitionSlug);
		return SaveObjectAsync(path, document, expectedGeneration, cancellationToken);
	}

	public Task<StorageDocument<DivisionDataDocument>> GetDivisionDataAsync(int year, string competitionSlug, string divisionKey, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetDivisionObjectPath(year, competitionSlug, divisionKey);
		return GetObjectAsync<DivisionDataDocument>(path, cancellationToken);
	}

	public Task SaveDivisionDataAsync(int year, string competitionSlug, string divisionKey, DivisionDataDocument document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		var path = DivisionKeyHelper.GetDivisionObjectPath(year, competitionSlug, divisionKey);
		return SaveObjectAsync(path, document, expectedGeneration, cancellationToken);
	}

	public async Task<IReadOnlyList<string>> ListCompetitionPathsAsync(CancellationToken cancellationToken = default)
	{
		var paths = new List<string>();
		await foreach (var obj in _storageClient.ListObjectsAsync(_bucketName, prefix: null).WithCancellation(cancellationToken))
		{
			if (obj.Name.EndsWith("/season.json", StringComparison.OrdinalIgnoreCase))
			{
				var path = obj.Name.Replace("/season.json", "", StringComparison.OrdinalIgnoreCase);
				paths.Add(path);
			}
		}

		return paths;
	}

	private async Task<StorageDocument<T>> GetObjectAsync<T>(string objectPath, CancellationToken cancellationToken)
	{
		try
		{
			var obj = await _storageClient.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
			await using var stream = new MemoryStream();
			await _storageClient.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
			stream.Position = 0;

			return new StorageDocument<T>
			{
				Content = await CompetitionJsonSerializer.DeserializeAsync<T>(stream, cancellationToken),
				Generation = obj.Generation,
				ObjectPath = objectPath
			};
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			throw new FileNotFoundException($"Storage object not found: {objectPath}", ex);
		}
	}

	private async Task SaveObjectAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
		{
			try
			{
				await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(CompetitionJsonSerializer.Serialize(document)));

				var uploadObjectOptions = new UploadObjectOptions
				{
					IfGenerationMatch = expectedGeneration
				};

				await _storageClient.UploadObjectAsync(_bucketName, objectPath, "application/json", stream, uploadObjectOptions, cancellationToken);
				return;
			}
			catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed)
			{
				if (attempt == MaxRetryAttempts - 1)
				{
					throw new StorageConcurrencyException(objectPath);
				}

				expectedGeneration = (await GetObjectAsync<T>(objectPath, cancellationToken)).Generation;
			}
		}
	}
}
