using Region42.ScoresStandings.Domain.Documents;

namespace Region42.ScoresStandings.Infrastructure.Storage;

internal static class StorageWriteLockHelper
{
	public const string LockPathPrefix = "admin/locks/";

	public static string GetLockObjectPath(string lockName)
	{
		return $"{LockPathPrefix}{lockName}.lock";
	}

	public static bool IsExpired(StorageWriteLockDocument document, DateTime utcNow)
	{
		return document.ExpiresAtUtc <= utcNow;
	}

	public static StorageWriteLockDocument CreateDocument(string holder, TimeSpan ttl, DateTime utcNow)
	{
		return new StorageWriteLockDocument
		{
			Holder = holder,
			AcquiredAtUtc = utcNow,
			ExpiresAtUtc = utcNow.Add(ttl)
		};
	}
}
