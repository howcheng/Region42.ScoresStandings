using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Infrastructure.Storage;

public class LocalFileStorageWriteLock : IStorageWriteLock
{
	private readonly string _localRoot;
	private readonly object _syncRoot = new();

	public LocalFileStorageWriteLock(IOptions<StorageOptions> options)
	{
		_localRoot = options.Value.LocalRoot;
	}

	public Task<bool> IsLockedAsync(string lockName, CancellationToken cancellationToken = default)
	{
		lock (_syncRoot)
		{
			var document = ReadLockDocument(lockName);
			if (document == null)
			{
				return Task.FromResult(false);
			}

			return Task.FromResult(!StorageWriteLockHelper.IsExpired(document, DateTime.UtcNow));
		}
	}

	public Task<IStorageWriteLockHandle?> TryAcquireAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		CancellationToken cancellationToken = default)
	{
		lock (_syncRoot)
		{
			var utcNow = DateTime.UtcNow;
			var existing = ReadLockDocument(lockName);
			if (existing != null)
			{
				if (!StorageWriteLockHelper.IsExpired(existing, utcNow))
				{
					return Task.FromResult<IStorageWriteLockHandle?>(null);
				}

				DeleteLockFile(lockName);
			}

			WriteLockDocument(lockName, StorageWriteLockHelper.CreateDocument(holder, ttl, utcNow));
			return Task.FromResult<IStorageWriteLockHandle?>(
				new StorageWriteLockHandle(lockName, () => ReleaseLockBestEffortAsync(lockName)));
		}
	}

	private string GetLockFilePath(string lockName)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName);
		return Path.Combine(_localRoot, objectPath.Replace('/', Path.DirectorySeparatorChar));
	}

	private StorageWriteLockDocument? ReadLockDocument(string lockName)
	{
		var path = GetLockFilePath(lockName);
		if (!File.Exists(path))
		{
			return null;
		}

		var json = File.ReadAllText(path);
		return CompetitionJsonSerializer.Deserialize<StorageWriteLockDocument>(json);
	}

	private void WriteLockDocument(string lockName, StorageWriteLockDocument document)
	{
		var path = GetLockFilePath(lockName);
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(path, CompetitionJsonSerializer.Serialize(document));
	}

	private void DeleteLockFile(string lockName)
	{
		var path = GetLockFilePath(lockName);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private Task ReleaseLockBestEffortAsync(string lockName)
	{
		lock (_syncRoot)
		{
			DeleteLockFile(lockName);
		}

		return Task.CompletedTask;
	}
}
