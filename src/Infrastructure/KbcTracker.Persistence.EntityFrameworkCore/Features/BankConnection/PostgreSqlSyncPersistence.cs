using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using KbcTracker.Domain.Features.Transactions.Repositories;
using Pivot.Framework.Domain.Repositories;
using Pivot.Framework.Infrastructure.Persistence.PostgreSQL.Locking;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Commits bank-sync batches and outcomes through Pivot while holding a database-wide sync lease.
/// </summary>
/// <param name="dbContext">The scoped PostgreSQL context.</param>
/// <param name="transactions">The permanent transaction history.</param>
/// <param name="sessions">The bank consents.</param>
/// <param name="unitOfWork">Commits the staged changes.</param>
public sealed class PostgreSqlSyncPersistence(
	KbcTrackerDbContext dbContext, ITransactionRepository transactions, IBankSessionRepository sessions, IUnitOfWork unitOfWork)
	: IBankSyncPersistence
{
	#region Static Instances
	/// <summary>The stable PostgreSQL lock shared by every backend process using this database.</summary>
	private static readonly long SyncLockId = PostgreSqlAdvisoryLock.ComputeLockId("KbcTracker.BankSync");
	#endregion

	#region IBankSyncPersistence Implementation
	/// <inheritdoc />
	public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) =>
		await PostgreSqlAdvisoryLock.TryAcquireAsync(dbContext, SyncLockId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public Task<SessionInfo?> LoadSessionAsync(CancellationToken cancellationToken) => sessions.GetCurrentAsync(cancellationToken);

	/// <inheritdoc />
	public Task<bool> HasTransactionsAsync(string accountUid, CancellationToken cancellationToken) =>
		transactions.HasAnyAsync(accountUid, cancellationToken);

	/// <inheritdoc />
	public async Task<int> MergeAsync(IEnumerable<Txn> incoming, CancellationToken cancellationToken)
	{
		var added = await transactions.StageMergeAsync(incoming, cancellationToken).ConfigureAwait(false);
		var result = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		if (result.IsFailure)
		{
			RestoreFailedBatch();
			throw new InvalidOperationException(result.Error.Message);
		}

		return added;
	}

	/// <inheritdoc />
	public async Task<bool> SaveIfCurrentAsync(SessionInfo session, CancellationToken cancellationToken)
	{
		if (!await sessions.IsCurrentAsync(session, cancellationToken).ConfigureAwait(false))
		{
			return false;
		}

		var stored = await sessions.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
		if (stored is null || stored.Id != session.Id || stored.ConnectedAt != session.ConnectedAt)
		{
			return false;
		}

		dbContext.Entry(stored).CurrentValues.SetValues(session);
		var result = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		if (result.IsFailure)
		{
			throw new InvalidOperationException(result.Error.Message);
		}

		return true;
	}
	#endregion

	#region Private Helpers
	/// <summary>Discards a failed batch so recording its failure cannot accidentally resubmit it.</summary>
	private void RestoreFailedBatch()
	{
		foreach (var entry in dbContext.ChangeTracker.Entries<Txn>().ToArray())
		{
			if (entry.State == EntityState.Added)
			{
				entry.State = EntityState.Detached;
			}
			else if (entry.State == EntityState.Modified)
			{
				entry.CurrentValues.SetValues(entry.OriginalValues);
				entry.State = EntityState.Unchanged;
			}
		}
	}
	#endregion
}
