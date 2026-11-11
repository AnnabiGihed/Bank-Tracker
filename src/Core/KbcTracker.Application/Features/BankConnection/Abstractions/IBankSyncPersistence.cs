namespace KbcTracker.Application.Features.BankConnection.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Persists bank-sync batches and outcomes without exposing a database context to the use case.
///              A lease excludes overlapping syncs across backend processes; committed batches never delete history.
/// </summary>
public interface IBankSyncPersistence
{
	#region Methods
	/// <summary>Attempts to exclude another sync until the returned lease is disposed.</summary>
	/// <param name="cancellationToken">Cancels acquiring the lease.</param>
	/// <returns>The lease, or null when another backend owns it.</returns>
	Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);

	/// <summary>Loads the current bank consent.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The current session, or null when no consent exists.</returns>
	Task<SessionInfo?> LoadSessionAsync(CancellationToken cancellationToken);

	/// <summary>Checks whether an account already has permanent history.</summary>
	/// <param name="accountUid">The account.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>True when a transaction of the account has been stored.</returns>
	Task<bool> HasTransactionsAsync(string accountUid, CancellationToken cancellationToken);

	/// <summary>Commits a downloaded batch, updating known ids and retaining all older transactions.</summary>
	/// <param name="incoming">The downloaded transactions.</param>
	/// <param name="cancellationToken">Cancels persistence.</param>
	/// <returns>The count of previously unknown ids durably stored.</returns>
	Task<int> MergeAsync(IEnumerable<Txn> incoming, CancellationToken cancellationToken);

	/// <summary>Saves an outcome only if its consent has not been replaced by a reconnect.</summary>
	/// <param name="session">The sync outcome.</param>
	/// <param name="cancellationToken">Cancels persistence.</param>
	/// <returns>True when saved, or false when the user has reconnected.</returns>
	Task<bool> SaveIfCurrentAsync(SessionInfo session, CancellationToken cancellationToken);
	#endregion
}
