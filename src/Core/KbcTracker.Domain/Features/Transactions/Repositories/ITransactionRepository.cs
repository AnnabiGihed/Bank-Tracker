using KbcTracker.Domain.Features.Transactions.Aggregates;

namespace KbcTracker.Domain.Features.Transactions.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Queries and stages permanent history without offering a deletion operation.
/// </summary>
public interface ITransactionRepository
{
	#region Methods
	/// <summary>Reads a stable page of history, newest booking date first.</summary>
	/// <param name="skip">The number of rows to skip.</param>
	/// <param name="take">The maximum rows to return.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The requested page of history.</returns>
	Task<IReadOnlyList<Txn>> ListAsync(int skip, int take, CancellationToken cancellationToken);

	/// <summary>Checks whether an account has history, without loading it.</summary>
	/// <param name="accountUid">The account.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>True when at least one transaction of the account is stored.</returns>
	Task<bool> HasAnyAsync(string accountUid, CancellationToken cancellationToken);

	/// <summary>Stages additions and updates by stable bank id, retaining older history.</summary>
	/// <param name="incoming">The downloaded batch; the final occurrence of a duplicate id wins.</param>
	/// <param name="cancellationToken">Cancels the read of existing ids.</param>
	/// <returns>The count of newly staged ids; the unit of work must commit before reporting them as saved.</returns>
	Task<int> StageMergeAsync(IEnumerable<Txn> incoming, CancellationToken cancellationToken);
	#endregion
}
