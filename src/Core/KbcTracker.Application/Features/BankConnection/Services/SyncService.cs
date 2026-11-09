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
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="SyncService"/>.
	/// </summary>
	/// <param name="bank">Where the transactions come from.</param>
	/// <param name="store">Where the session and the transaction history are kept.</param>
	/// <param name="settings">Sync settings; <see cref="SyncSettings.HistoryMonths"/> bounds a full sync.</param>
	/// <param name="log">Logger for sync progress and failures.</param>
	public SyncService(IBankTransactionSource bank, IStore store, SyncSettings settings, ILogger<SyncService> log)
	{
		_bank = bank;
		_store = store;
		_settings = settings;
		_log = log;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Downloads the transactions of every connected account and merges them into the store. Records the time of
	/// the sync, or its error, in the session.
	/// </summary>
	/// <param name="full"><see langword="true"/> to ask for the longest history the bank allows; otherwise the last
	/// 30 days, since everything older is already stored. The first sync is always full.</param>
	/// <returns>The number of new transactions and <see langword="null"/>, or 0 and the error message (at most 300
	/// characters) when the user is not connected or the bank call failed. Exceptions are not rethrown.</returns>
	public async Task<(int Added, string? Error)> SyncAsync(bool full = false)
	{
		var session = _store.LoadSession();
		if (session is null)
		{
			return (0, "Not connected to the bank yet.");
		}

		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			full = full || _store.LoadTransactions().Count == 0;
			var from = full
				? DateOnly.FromDateTime(DateTime.Today.AddMonths(-_settings.HistoryMonths))
				: DateOnly.FromDateTime(DateTime.Today.AddDays(-30));

			var added = 0;
			foreach (var uid in session.AccountUids)
			{
				var transactions = await DownloadAsync(uid, from, full).ConfigureAwait(false);
				added += _store.Merge(transactions);
			}

			session.LastSync = DateTimeOffset.UtcNow;
			session.LastError = null;
			_store.SaveSession(session);
			LogSyncDone(added);
			return (added, null);
		}
		catch (Exception ex)
		{
			// Reported to the user through the session instead of thrown: background syncs have no caller to catch it.
			LogSyncFailed(ex);
			session.LastError = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
			_store.SaveSession(session);
			return (0, session.LastError);
		}
		finally
		{
			Gate.Release();
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Downloads one account's transactions. When the bank refuses an incremental request, retries with
	/// <c>strategy=longest</c>, which it always accepts.
	/// </summary>
	private async Task<IReadOnlyList<Txn>> DownloadAsync(string accountUid, DateOnly from, bool full)
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
