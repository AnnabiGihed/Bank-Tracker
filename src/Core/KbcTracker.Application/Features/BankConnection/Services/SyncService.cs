using Microsoft.Extensions.Logging;

namespace KbcTracker.Application.Features.BankConnection.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Downloads new transactions of every connected account from the bank (through
///              <see cref="IBankTransactionSource"/>) and adds them to the permanent history.
///
///              One sync runs at a time per process (a static gate), because the web host can start one from a page
///              and from its timer at the same moment. The bank returns only about 90 days, so a sync never removes
///              anything from the store.
/// </summary>
public sealed partial class SyncService
{
	#region Constants
	/// <summary>
	/// The answer when there is no bank connection to sync.
	/// </summary>
	private const string NotConnected = "Not connected to the bank yet.";
	#endregion

	#region Static Instances
	/// <summary>
	/// Lets one sync run at a time in this process.
	/// </summary>
	private static readonly SemaphoreSlim Gate = new(1, 1);
	#endregion

	#region Fields
	private readonly IBankTransactionSource _bank;
	private readonly IStore _store;
	private readonly SyncSettings _settings;
	private readonly ILogger<SyncService> _log;
	private readonly TimeProvider _clock;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="SyncService"/>.
	/// </summary>
	/// <param name="bank">Where the transactions come from.</param>
	/// <param name="store">Where the session and the transaction history are kept.</param>
	/// <param name="settings">Sync settings; <see cref="SyncSettings.HistoryMonths"/> bounds a full sync.</param>
	/// <param name="log">Logger for sync progress and failures.</param>
	/// <param name="clock">The clock that dates the sync and the history window.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public SyncService(IBankTransactionSource bank, IStore store, SyncSettings settings, ILogger<SyncService> log, TimeProvider clock)
	{
		_bank = bank ?? throw new ArgumentNullException(nameof(bank));
		_store = store ?? throw new ArgumentNullException(nameof(store));
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
	/// 30 days, since everything older is already stored. The first sync is always full.</param>
	/// <returns>The number of new transactions and <see langword="null"/> when the sync succeeded. Otherwise an error
	/// message of at most <see cref="SessionInfo.MaxErrorLength"/> characters, when the user is not connected or a bank,
	/// file or storage error stopped the sync. Transactions merged before a failure are kept; the count returned with
	/// an error is that number only when saving the outcome was the one thing that failed, and 0 otherwise.
	/// Exceptions are not rethrown.</returns>
	public async Task<(int Added, string? Error)> SyncAsync(bool full = false)
	{
		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			// Read only once the gate is held: a sync that ran meanwhile may have changed the session, and working on
			// an older copy would undo it, for example bring back a left-out count a full sync just cleared.
			var session = _store.LoadSession();
			return session is null ? (0, NotConnected) : await SyncSessionAsync(session, full).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Reading or saving the session failed, so the failure cannot be recorded in it: returned instead of
			// thrown, as background syncs have no caller to catch it.
			LogSyncFailed(ex);
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
	private async Task<(int Added, string? Error)> SyncSessionAsync(SessionInfo session, bool full)
	{
		var fullSync = full || _store.LoadTransactions().Count == 0;
		(int Added, int Skipped) result;
		try
		{
			result = await DownloadAllAsync(session, fullSync).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// Every failure of the download, file errors included (a missing private key is one), is recorded in the
			// session so the pages show it. If saving it fails too, the caller reports that error instead.
			LogSyncFailed(ex);
			session.RecordFailure(ex.Message);
			SaveIfStillCurrent(session);
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
			SaveIfStillCurrent(session);
		}
		catch (Exception ex)
		{
			// The transactions are merged; only the sync time and left-out count could not be saved. Reported with the
			// number added, and the stored session keeps its previous state: the next sync records the time again,
			// but a full sync's reset of the left-out count needs another full sync ("Sync all again").
			LogSyncFailed(ex);
			return (result.Added, SessionInfo.ShortenError(ex.Message));
		}

		LogSyncDone(result.Added);
		return (result.Added, null);
	}

	/// <summary>
	/// Downloads and merges the transactions of every account of the session.
	/// </summary>
	private async Task<(int Added, int Skipped)> DownloadAllAsync(SessionInfo session, bool fullSync)
	{
		var today = DateOnly.FromDateTime(_clock.GetLocalNow().Date);
		var from = fullSync ? today.AddMonths(-_settings.HistoryMonths) : today.AddDays(-30);
		var added = 0;
		var skipped = 0;
		foreach (var uid in session.AccountUids)
		{
			var download = await DownloadAsync(uid, from, fullSync).ConfigureAwait(false);
			added += _store.Merge(download.Transactions);
			skipped += download.Skipped;
		}

		return (added, skipped);
	}

	/// <summary>
	/// Saves the outcome of a sync, unless the user reconnected while it ran: the new connection then replaced the
	/// session (another id, or the same id connected again), and the outcome of a sync of the old one must not
	/// overwrite it.
	/// </summary>
	private void SaveIfStillCurrent(SessionInfo session)
	{
		var stored = _store.LoadSession();
		if (stored is not null && stored.Id == session.Id && stored.ConnectedAt == session.ConnectedAt)
		{
			_store.SaveSession(session);
		}
		else
		{
			LogSessionReplaced();
		}
	}

	/// <summary>
	/// Downloads one account's transactions. When the bank refuses an incremental request, retries with
	/// <c>strategy=longest</c>, which it always accepts.
	/// </summary>
	private async Task<BankDownload> DownloadAsync(string accountUid, DateOnly from, bool full)
	{
		try
		{
			return await _bank.GetTransactionsAsync(accountUid, from, longest: full).ConfigureAwait(false);
		}
		catch (InvalidOperationException ex) when (!full)
		{
			LogIncrementalRefused(ex);
			return await _bank.GetTransactionsAsync(accountUid, null, longest: true).ConfigureAwait(false);
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
	/// Logs a failed sync with its exception.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Error, Message = "Sync failed")]
	private partial void LogSyncFailed(Exception exception);

	/// <summary>
	/// Logs that the bank refused an incremental request, before the retry with <c>strategy=longest</c>.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Warning, Message = "Incremental sync refused, retrying with strategy=longest")]
	private partial void LogIncrementalRefused(Exception exception);
	#endregion
}
