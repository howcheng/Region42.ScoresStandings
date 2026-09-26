namespace Region42.ScoresStandings.Application.Exceptions;

public class StorageWriteLockHeldException : Exception
{
	public StorageWriteLockHeldException(string lockName)
		: base($"Write lock '{lockName}' is held by another process. Try again shortly.")
	{
		LockName = lockName;
	}

	public string LockName { get; }
}
