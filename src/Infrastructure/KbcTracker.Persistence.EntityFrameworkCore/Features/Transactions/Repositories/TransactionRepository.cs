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
		var batch = new Dictionary<(string AccountUid, TxnId Id), Txn>();
		foreach (var transaction in incoming)
		{
			batch[(transaction.AccountUid, transaction.Id)] = transaction;
		}

		// The bank's reference is unique within an account only, so one of another account can hold the id: such a
		// transaction is stored under the account's id as a prefix, and the one already stored is never rewritten.
		// The same rule applies inside one batch, so the choice is the same on every sync of the same transactions.
		var ids = batch.Values.Select(t => t.Id).Concat(batch.Values.Select(PrefixedId)).Distinct().ToArray();
		var existing = await dbContext.Transactions.Where(t => ids.Contains(t.Id))
			.ToDictionaryAsync(t => t.Id, cancellationToken).ConfigureAwait(false);
		var claimed = new Dictionary<TxnId, string>();
		var added = 0;
		foreach (var received in batch.Values.OrderBy(t => t.AccountUid, StringComparer.Ordinal).ThenBy(t => t.Id.Value, StringComparer.Ordinal))
		{
			var transaction = received;
			existing.TryGetValue(transaction.Id, out var stored);
			var owner = stored?.AccountUid ?? (claimed.TryGetValue(transaction.Id, out var claimant) ? claimant : null);
			if (owner is not null && !string.Equals(owner, transaction.AccountUid, StringComparison.Ordinal))
			{
				transaction = transaction.WithId(PrefixedId(transaction));
				existing.TryGetValue(transaction.Id, out stored);
			}

			claimed[transaction.Id] = transaction.AccountUid;
			if (stored is not null)
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

	#region Private Helpers
	/// <summary>
	/// Gives the id a transaction gets when its bank reference is taken by another account: the account's id before it.
	/// </summary>
	private static TxnId PrefixedId(Txn transaction) => new($"{transaction.AccountUid}:{transaction.Id.Value}");
	#endregion
}
