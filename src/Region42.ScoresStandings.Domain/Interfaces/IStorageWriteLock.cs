namespace Region42.ScoresStandings.Domain.Interfaces;

/// <summary>
/// Coarse-grained write lock stored in competition storage (GCS or local files).
/// </summary>
public interface IStorageWriteLock
{
	/// <summary>
	/// Returns true when an unexpired lock object exists for the given name.
	/// </summary>
	Task<bool> IsLockedAsync(string lockName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Attempts to acquire the lock. Returns null when another holder owns an unexpired lock.
	/// </summary>
	Task<IStorageWriteLockHandle?> TryAcquireAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an acquired storage write lock. Disposing releases the lock.
/// </summary>
public interface IStorageWriteLockHandle : IAsyncDisposable
{
	string LockName { get; }
}
