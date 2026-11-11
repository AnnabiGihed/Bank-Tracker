using Microsoft.Extensions.Logging;

namespace KbcTracker.Application.Features.BankConnection.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Downloads new transactions of every connected account from the bank (through
///              <see cref="IBankTransactionSource"/>) and adds them to the permanent history.
///
///              One sync runs at a time per process and through a persistence lease, because a request and the
///              automatic sync can start one at the same moment. The bank returns only about 90 days, so a sync never
///              removes anything from the store.
/// </summary>
public sealed partial class SyncService
{
	#region Constants
	/// <summary>
	/// The answer when there is no bank connection to sync.
	/// </summary>
	private const string NotConnected = "Not connected to the bank yet.";

	/// <summary>
	/// How many days back an incremental sync asks for.
	/// </summary>
	private const int IncrementalDays = 30;
	#endregion

	#region Static Instances
	/// <summary>
	/// Lets one sync run at a time in this process.
	/// </summary>
	private static readonly SemaphoreSlim Gate = new(1, 1);
	#endregion

	#region Fields
	private readonly IBankTransactionSource _bank;
	private readonly IBankSyncPersistence _persistence;
	private readonly SyncSettings _settings;
	private readonly ILogger<SyncService> _log;
	private readonly TimeProvider _clock;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="SyncService"/>.
	/// </summary>
	/// <param name="bank">Where the transactions come from.</param>
	/// <param name="persistence">Where sync batches and outcomes are committed.</param>
	/// <param name="settings">Sync settings; <see cref="SyncSettings.HistoryMonths"/> bounds a full sync.</param>
	/// <param name="log">Logger for sync progress and failures.</param>
	/// <param name="clock">The clock that dates the sync and the history window.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public SyncService(IBankTransactionSource bank, IBankSyncPersistence persistence, SyncSettings settings, ILogger<SyncService> log, TimeProvider clock)
	{
		_bank = bank ?? throw new ArgumentNullException(nameof(bank));
		_persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_log = log ?? throw new ArgumentNullException(nameof(log));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Downloads the transactions of every connected account and merges them into the store. Records the time of
	/// the sync and how many transactions could not be read, or its error, in the session.
	/// </summary>
	/// <param name="full"><see langword="true"/> to ask for the longest history the bank allows; otherwise the last
	/// 30 days, since everything older is already stored. The sync is full anyway when an account has no history yet
	/// (the first sync, or an account added at a reconnect), or when the last successful sync is missing or older than
	/// those 30 days (after a consent gap or a failed sync), so that no day the bank still holds is skipped.</param>
	/// <returns>The number of new transactions and <see langword="null"/> when the sync succeeded. Otherwise an error
	/// message of at most <see cref="SessionInfo.MaxErrorLength"/> characters, when the user is not connected or a bank,
	/// file or storage error stopped the sync. Transactions merged before a failure are kept; the count returned with
	/// an error is that number only when saving the outcome was the one thing that failed, and 0 otherwise.
	/// Storage failures are returned; cancellation propagates.</returns>
	/// <param name="cancellationToken">Cancels waits and persistence; an in-flight bank request may still finish.</param>
	/// <exception cref="OperationCanceledException">The sync was cancelled.</exception>
	public async Task<(int Added, string? Error)> SyncAsync(bool full = false, CancellationToken cancellationToken = default)
	{
		await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Read only once the gate is held: a sync that ran meanwhile may have changed the session, and working on
			// an older copy would undo it, for example bring back a left-out count a full sync just cleared.
			var lease = await _persistence.TryAcquireAsync(cancellationToken).ConfigureAwait(false);
			if (lease is null)
			{
				return (0, "A bank sync is already running.");
			}

			await using (lease.ConfigureAwait(false))
			{
				var session = await _persistence.LoadSessionAsync(cancellationToken).ConfigureAwait(false);
				return session is null ? (0, NotConnected) : await SyncSessionAsync(session, full, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Reading or saving the session failed, so the failure cannot be recorded in it: returned instead of
			// thrown, as background syncs have no caller to catch it.
			LogSyncFailed(ex.GetType().Name);
			return (0, SessionInfo.ShortenError(ex.Message));
		}
		finally
		{
			Gate.Release();
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Downloads and merges every account of the session, then records the outcome in it.
	/// </summary>
	private async Task<(int Added, string? Error)> SyncSessionAsync(SessionInfo session, bool full, CancellationToken cancellationToken)
	{
		var fullSync = full || !IsCoveredByIncremental(session.LastSync)
			|| !await EveryAccountHasHistoryAsync(session, cancellationToken).ConfigureAwait(false);
		(int Added, int Skipped) result;
		try
		{
			result = await DownloadAllAsync(session, fullSync, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Every failure of the download, file errors included (a missing private key is one), is recorded in the
			// session so the pages show it. If saving it fails too, the caller reports that error instead.
			LogSyncFailed(ex.GetType().Name);
			session.RecordFailure(ex.Message);
			await SaveIfStillCurrentAsync(session, cancellationToken).ConfigureAwait(false);
			return (0, session.LastError);
		}

		if (fullSync)
		{
			session.RecordFullSync(_clock.GetUtcNow(), result.Skipped);
		}
		else
		{
			session.RecordIncrementalSync(_clock.GetUtcNow(), result.Skipped);
		}

		try
		{
			await SaveIfStillCurrentAsync(session, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// The transactions are merged; only the sync time and left-out count could not be saved. Reported with the
			// number added, and the stored session keeps its previous state: the next sync records the time again,
			// but a full sync's reset of the left-out count needs another full sync ("Sync all again").
			LogSyncFailed(ex.GetType().Name);
			return (result.Added, SessionInfo.ShortenError(ex.Message));
		}

		LogSyncDone(result.Added);
		return (result.Added, null);
	}

	/// <summary>
	/// Tells whether an incremental sync reaches back to the last successful sync, with a day's margin for time
	/// zones. When it does not, the days in between would never be fetched and would be lost once they leave the
	/// bank's 90-day window.
	/// </summary>
	private bool IsCoveredByIncremental(DateTimeOffset? lastSync) =>
		lastSync is not null && lastSync.Value > _clock.GetUtcNow().AddDays(-(IncrementalDays - 1));

	/// <summary>
	/// Tells whether every account of the session already has history. An account added at a reconnect has none, and
	/// an incremental sync would give it only 30 days although the bank holds about 90.
	/// </summary>
	private async Task<bool> EveryAccountHasHistoryAsync(SessionInfo session, CancellationToken cancellationToken)
	{
		foreach (var uid in session.AccountUids)
		{
			if (!await _persistence.HasTransactionsAsync(uid, cancellationToken).ConfigureAwait(false))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Downloads and merges the transactions of every account of the session.
	/// </summary>
	private async Task<(int Added, int Skipped)> DownloadAllAsync(SessionInfo session, bool fullSync, CancellationToken cancellationToken)
	{
		var today = DateOnly.FromDateTime(_clock.GetLocalNow().Date);
		var from = fullSync ? today.AddMonths(-_settings.HistoryMonths) : today.AddDays(-IncrementalDays);
		var added = 0;
		var skipped = 0;
		foreach (var uid in session.AccountUids)
		{
			var download = await DownloadAsync(uid, from, fullSync, cancellationToken).ConfigureAwait(false);
			added += await _persistence.MergeAsync(download.Transactions, cancellationToken).ConfigureAwait(false);
			skipped += download.Skipped;
		}

		return (added, skipped);
	}

	/// <summary>
	/// Saves the outcome of a sync, unless the user reconnected while it ran: the new connection then replaced the
	/// session (another id, or the same id connected again), and the outcome of a sync of the old one must not
	/// overwrite it.
	/// </summary>
	private async Task SaveIfStillCurrentAsync(SessionInfo session, CancellationToken cancellationToken)
	{
		if (!await _persistence.SaveIfCurrentAsync(session, cancellationToken).ConfigureAwait(false))
		{
			LogSessionReplaced();
		}
	}

	/// <summary>
	/// Downloads one account's transactions. When the bank refuses an incremental request, retries with
	/// <c>strategy=longest</c>, which it always accepts.
	/// </summary>
	private async Task<BankDownload> DownloadAsync(string accountUid, DateOnly from, bool full, CancellationToken cancellationToken)
	{
		try
		{
			return await _bank.GetTransactionsAsync(accountUid, from, longest: full).WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (InvalidOperationException ex) when (!full)
		{
			LogIncrementalRefused(ex.GetType().Name);
			return await _bank.GetTransactionsAsync(accountUid, null, longest: true).WaitAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Logs a successful sync.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Information, Message = "Sync done, {Added} new transactions")]
	private partial void LogSyncDone(int added);

	/// <summary>
	/// Logs that the outcome of a sync was not saved because the user reconnected while it ran.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Information, Message = "Sync outcome not saved: the bank connection was replaced while it ran")]
	private partial void LogSessionReplaced();

	/// <summary>
	/// Logs a failed sync by its exception type only: provider messages can carry response bodies with account
	/// details, and the shortened message is already kept on the connection.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Error, Message = "Sync failed ({ExceptionType})")]
	private partial void LogSyncFailed(string exceptionType);

	/// <summary>
	/// Logs that the bank refused an incremental request, before the retry with <c>strategy=longest</c>.
	/// </summary>
	/// <remarks>Only the exception type is logged, for the same reason as <see cref="LogSyncFailed"/>.</remarks>
	[LoggerMessage(Level = LogLevel.Warning, Message = "Incremental sync refused ({ExceptionType}), retrying with strategy=longest")]
	private partial void LogIncrementalRefused(string exceptionType);
	#endregion
}
