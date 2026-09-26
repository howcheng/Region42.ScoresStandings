using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Infrastructure.Storage;

internal sealed class StorageWriteLockHandle : IStorageWriteLockHandle
{
	private readonly Func<Task> _releaseAsync;
	private int _released;

	public StorageWriteLockHandle(string lockName, Func<Task> releaseAsync)
	{
		LockName = lockName;
		_releaseAsync = releaseAsync;
	}

	public string LockName { get; }

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.CompareExchange(ref _released, 1, 0) != 0)
		{
			return;
		}

		await _releaseAsync();
	}
}
