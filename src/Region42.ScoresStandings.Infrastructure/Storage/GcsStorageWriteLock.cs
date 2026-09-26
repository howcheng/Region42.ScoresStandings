using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Infrastructure.Storage;

public class GcsStorageWriteLock : IStorageWriteLock
{
	private readonly StorageClient _storageClient;
	private readonly string _bucketName;

	public GcsStorageWriteLock(StorageClient storageClient, IOptions<StorageOptions> options)
	{
		_storageClient = storageClient;
		_bucketName = options.Value.BucketName;
	}

	public async Task<bool> IsLockedAsync(string lockName, CancellationToken cancellationToken = default)
	{
		var document = await TryReadLockAsync(lockName, cancellationToken);
		return document != null;
	}

	public async Task<IStorageWriteLockHandle?> TryAcquireAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; attempt < 3; attempt++)
		{
			var utcNow = DateTime.UtcNow;
			var existing = await TryReadLockDocumentAsync(lockName, cancellationToken);
			if (existing != null)
			{
				if (!StorageWriteLockHelper.IsExpired(existing.Value.Document, utcNow))
				{
					return null;
				}

				await DeleteLockAsync(lockName, existing.Value.Generation, cancellationToken);
			}

			if (await TryCreateLockAsync(lockName, holder, ttl, utcNow, cancellationToken))
			{
				return new StorageWriteLockHandle(lockName, () => ReleaseLockBestEffortAsync(lockName));
			}
		}

		return null;
	}

	private async Task<StorageWriteLockDocument?> TryReadLockAsync(string lockName, CancellationToken cancellationToken)
	{
		var existing = await TryReadLockDocumentAsync(lockName, cancellationToken);
		if (existing == null)
		{
			return null;
		}

		return StorageWriteLockHelper.IsExpired(existing.Value.Document, DateTime.UtcNow)
			? null
			: existing.Value.Document;
	}

	private async Task<(StorageWriteLockDocument Document, long Generation)?> TryReadLockDocumentAsync(
		string lockName,
		CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName);

		try
		{
			var obj = await _storageClient.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
			await using var stream = new MemoryStream();
			await _storageClient.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
			stream.Position = 0;

			var document = await CompetitionJsonSerializer.DeserializeAsync<StorageWriteLockDocument>(stream, cancellationToken);
			return (document, obj.Generation ?? 0L);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	private async Task<bool> TryCreateLockAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		DateTime utcNow,
		CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName);
		var document = StorageWriteLockHelper.CreateDocument(holder, ttl, utcNow);

		try
		{
			await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(CompetitionJsonSerializer.Serialize(document)));
			var uploadObjectOptions = new UploadObjectOptions
			{
				IfGenerationMatch = 0
			};

			await _storageClient.UploadObjectAsync(
				_bucketName,
				objectPath,
				"application/json",
				stream,
				uploadObjectOptions,
				cancellationToken);

			return true;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.PreconditionFailed)
		{
			return false;
		}
	}

	private async Task DeleteLockAsync(string lockName, long generation, CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName);

		try
		{
			await _storageClient.DeleteObjectAsync(
				_bucketName,
				objectPath,
				new DeleteObjectOptions { Generation = generation },
				cancellationToken);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
		{
		}
	}

	private async Task ReleaseLockBestEffortAsync(string lockName)
	{
		try
		{
			var existing = await TryReadLockDocumentAsync(lockName, CancellationToken.None);
			if (existing == null)
			{
				return;
			}

			await DeleteLockAsync(lockName, existing.Value.Generation, CancellationToken.None);
		}
		catch (GoogleApiException)
		{
		}
	}
}
