using Microsoft.EntityFrameworkCore.Storage;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Web.Data;

/// <summary>
/// Wrapper for Entity Framework Core's IDbContextTransaction that implements our domain interface.
/// </summary>
internal class DbTransactionWrapper : IDbTransaction
{
	private readonly IDbContextTransaction _transaction;

	public DbTransactionWrapper(IDbContextTransaction transaction)
	{
		_transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
	}

	public Task CommitAsync(CancellationToken cancellationToken = default)
	{
		return _transaction.CommitAsync(cancellationToken);
	}

	public Task RollbackAsync(CancellationToken cancellationToken = default)
	{
		return _transaction.RollbackAsync(cancellationToken);
	}

	public ValueTask DisposeAsync()
	{
		return _transaction.DisposeAsync();
	}
}
