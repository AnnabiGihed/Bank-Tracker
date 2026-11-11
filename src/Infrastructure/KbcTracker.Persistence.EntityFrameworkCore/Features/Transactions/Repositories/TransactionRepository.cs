using KbcTracker.Domain.Features.Transactions.Repositories;
using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Transactions.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Implements ITransactionRepository over the scoped PostgreSQL context.
/// </summary>
/// <param name="dbContext">The scoped context whose changes the unit of work commits.</param>
internal sealed class TransactionRepository(KbcTrackerDbContext dbContext) : ITransactionRepository
{
	#region ITransactionRepository Implementation
	/// <inheritdoc />
	public async Task<IReadOnlyList<Txn>> ListAsync(int skip, int take, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(skip);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
		return await dbContext.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenBy(t => t.Id)
			.Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<bool> HasAnyAsync(string accountUid, CancellationToken cancellationToken) =>
		dbContext.Transactions.AnyAsync(t => t.AccountUid == accountUid, cancellationToken);

	/// <inheritdoc />
	public async Task<int> StageMergeAsync(IEnumerable<Txn> incoming, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(incoming);
		var byId = new Dictionary<TxnId, Txn>();
		foreach (var transaction in incoming)
		{
			byId[transaction.Id] = transaction;
		}

		var ids = byId.Keys.ToArray();
		var existing = await dbContext.Transactions.Where(t => ids.Contains(t.Id))
			.ToDictionaryAsync(t => t.Id, cancellationToken).ConfigureAwait(false);
		var added = 0;
		foreach (var transaction in byId.Values)
		{
			if (existing.TryGetValue(transaction.Id, out var stored))
			{
				dbContext.Entry(stored).CurrentValues.SetValues(transaction);
			}
			else
			{
				dbContext.Transactions.Add(transaction);
				added++;
			}
		}

		return added;
	}
	#endregion
}
