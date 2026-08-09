namespace Region42.ScoresStandings.Domain.Interfaces;

/// <summary>
/// Represents a database transaction that can be committed or rolled back.
/// </summary>
public interface IDbTransaction : IAsyncDisposable
{
	/// <summary>
	/// Commits all changes made during this transaction.
	/// </summary>
	Task CommitAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Rolls back all changes made during this transaction.
	/// </summary>
	Task RollbackAsync(CancellationToken cancellationToken = default);
}
