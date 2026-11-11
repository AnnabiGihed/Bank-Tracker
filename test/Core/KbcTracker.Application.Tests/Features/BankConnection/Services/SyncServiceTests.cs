using Microsoft.Extensions.Logging.Abstractions;

namespace KbcTracker.Application.Tests.Features.BankConnection.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that <see cref="SyncService"/> downloads every connected account, merges the
///              transactions, records the outcome in the session, and falls back to the longest history when the
///              bank refuses an incremental request.
///
///              The bank is an NSubstitute double of <see cref="IBankTransactionSource"/>; the storage is an
///              in-memory <see cref="IBankSyncPersistence"/> that merges by id like the real one.
/// </summary>
public sealed class SyncServiceTests
{
	#region Test Infrastructure
	/// <summary>
	/// The bank double of the test.
	/// </summary>
	private readonly IBankTransactionSource _bank = Substitute.For<IBankTransactionSource>();

	/// <summary>
	/// Creates the sync service over the bank double and the given store.
	/// </summary>
	private SyncService Sync(InMemoryStore store) =>
		new(_bank, store, new SyncSettings(HistoryMonths: 13), NullLogger<SyncService>.Instance, TimeProvider.System);

	/// <summary>
	/// A booked debit of 10 € in the given account.
	/// </summary>
	private static Txn Debit(string id, string accountUid) =>
		Txn.Rehydrate(new TxnId(id), accountUid, new DateOnly(2026, 11, 2), Money.Rehydrate(-10m, "EUR"), Counterparty.Rehydrate("Shop", null), "Test", BankRecord.None);

	/// <summary>
	/// A download in which every transaction could be read.
	/// </summary>
	private static BankDownload Download(params Txn[] transactions) => new(transactions, Skipped: 0);

	/// <summary>
	/// A bank connection to the given accounts that has never synced.
	/// </summary>
	private static SessionInfo Connected(params string[] accountUids) =>
		SessionInfo.Rehydrate(new BankSessionId("s"), accountUids, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero), lastSync: null, lastError: null, skippedTransactions: 0);

	/// <summary>
	/// A bank connection to the given accounts whose last successful sync ended the given time ago.
	/// </summary>
	private static SessionInfo SyncedAgo(TimeSpan age, params string[] accountUids)
	{
		var session = Connected(accountUids);
		session.RecordIncrementalSync(DateTimeOffset.UtcNow - age, 0);
		return session;
	}
	#endregion

	#region Sync Tests
	/// <summary>Verifies another backend's lease prevents bank reads and persistence changes.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_LeaseUnavailable_ReturnsConflictWithoutCallingBank()
	{
		var persistence = Substitute.For<IBankSyncPersistence>();
		persistence.TryAcquireAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(null));
		var sync = new SyncService(_bank, persistence, new SyncSettings(13), NullLogger<SyncService>.Instance, TimeProvider.System);

		var (added, error) = await sync.SyncAsync();

		added.Should().Be(0);
		error.Should().Be("A bank sync is already running.");
		await persistence.DidNotReceiveWithAnyArgs().LoadSessionAsync(default);
		await _bank.DidNotReceiveWithAnyArgs().GetTransactionsAsync(default!, default, default);
	}

	/// <summary>Verifies cancellation releases both gates without committing a bank response received afterwards.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_CancelledBankWait_ReleasesLeaseWithoutSaving()
	{
		var persistence = Substitute.For<IBankSyncPersistence>();
		var lease = Substitute.For<IAsyncDisposable>();
		persistence.TryAcquireAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(lease));
		persistence.LoadSessionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<SessionInfo?>(Connected("current")));
		var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var download = new TaskCompletionSource<BankDownload>(TaskCreationOptions.RunContinuationsAsynchronously);
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true).Returns(_ =>
		{
			started.SetResult();
			return download.Task;
		});
		using var cancellation = new CancellationTokenSource();
		var sync = new SyncService(_bank, persistence, new SyncSettings(13), NullLogger<SyncService>.Instance, TimeProvider.System);
		var pending = sync.SyncAsync(cancellationToken: cancellation.Token);
		await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

		await cancellation.CancelAsync();
		var cancelled = async () => await pending.ConfigureAwait(false);
		await cancelled.Should().ThrowAsync<OperationCanceledException>();
		download.SetResult(Download(Debit("late", "current")));
		await lease.Received(1).DisposeAsync();
		await persistence.DidNotReceiveWithAnyArgs().MergeAsync(default!, default);
		await persistence.DidNotReceiveWithAnyArgs().SaveIfCurrentAsync(default!, default);
		var next = await Sync(new InMemoryStore()).SyncAsync().WaitAsync(TimeSpan.FromSeconds(10));
		next.Error.Should().Be("Not connected to the bank yet.");
	}

	/// <summary>
	/// Verifies that a sync without a bank session does nothing and says so.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_NotConnected_ReturnsErrorWithoutCallingBank()
	{
		var (added, error) = await Sync(new InMemoryStore()).SyncAsync();

		added.Should().Be(0);
		error.Should().Be("Not connected to the bank yet.");
		await _bank.DidNotReceiveWithAnyArgs().GetTransactionsAsync(default!, default, default);
	}

	/// <summary>
	/// Verifies that every connected account is downloaded and merged, and that the sync time is recorded.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_TwoAccounts_MergesBothAndRecordsLastSync()
	{
		var store = new InMemoryStore { Session = Connected("current", "savings") };
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns(Download(Debit("tx-current", "current")));
		_bank.GetTransactionsAsync("savings", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns(Download(Debit("tx-savings", "savings")));

		var (added, error) = await Sync(store).SyncAsync();

		error.Should().BeNull();
		added.Should().Be(2);
		store.Transactions.Select(t => t.AccountUid).Should().BeEquivalentTo("current", "savings");
		store.Session.LastSync.Should().NotBeNull();
	}

	/// <summary>
	/// Verifies that the first sync asks for the longest history the bank allows.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_EmptyHistory_AsksForLongestHistory()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download(Debit("tx-1", "current")));

		await Sync(store).SyncAsync();

		await _bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true);
	}

	/// <summary>
	/// Verifies that a sync is full, although history exists, when the last successful sync is missing or older than
	/// the incremental window: after a consent gap or a failed sync, a 30-day request would skip days the bank still
	/// holds. A recent sync stays incremental.
	/// </summary>
	/// <param name="lastSyncDaysAgo">Days since the last successful sync, or a negative value for never.</param>
	/// <param name="expectedLongest">Whether the longest history is expected.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData(-1, true)]
	[InlineData(40, true)]
	[InlineData(30, true)]
	[InlineData(28, false)]
	[InlineData(1, false)]
	public async Task SyncAsync_HistoryExists_FullWhenLastSyncTooOld(int lastSyncDaysAgo, bool expectedLongest)
	{
		var session = lastSyncDaysAgo < 0 ? Connected("current") : SyncedAgo(TimeSpan.FromDays(lastSyncDaysAgo), "current");
		var store = new InMemoryStore { Session = session };
		store.Transactions.Add(Debit("tx-old", "current"));
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download(Debit("tx-new", "current")));

		await Sync(store).SyncAsync();

		await _bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), expectedLongest);
	}

	/// <summary>
	/// Verifies that an account added at a reconnect, which has no history yet, gets a full sync although the other
	/// account synced recently: an incremental sync would give it only 30 of the bank's 90 days.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_AccountWithoutHistory_FullSync()
	{
		var store = new InMemoryStore { Session = SyncedAgo(TimeSpan.FromHours(1), "current", "savings") };
		store.Transactions.Add(Debit("tx-old", "current"));
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download());

		await Sync(store).SyncAsync();

		await _bank.Received(1).GetTransactionsAsync("savings", Arg.Any<DateOnly?>(), true);
		await _bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true);
		store.Session.SkippedTransactions.Should().Be(0, "the whole run was full, so its left-out count replaces the old one");
	}

	/// <summary>
	/// Verifies that when the bank refuses an incremental request, the sync retries once with the longest history
	/// and still succeeds.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_IncrementalRefused_RetriesWithLongest()
	{
		var store = new InMemoryStore { Session = SyncedAgo(TimeSpan.FromHours(1), "current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		_bank.GetTransactionsAsync("current", Arg.Is<DateOnly?>(d => d != null), false)
			.Returns<BankDownload>(_ => throw new InvalidOperationException("WRONG_TRANSACTIONS_PERIOD"));
		_bank.GetTransactionsAsync("current", null, true).Returns(Download(Debit("tx-new", "current")));

		var (added, error) = await Sync(store).SyncAsync();

		error.Should().BeNull();
		added.Should().Be(1);
		await _bank.Received(1).GetTransactionsAsync("current", null, true);
	}

	/// <summary>
	/// Verifies that transactions the bank sent but that could not be read are counted in the session, so the user
	/// is told, while the readable ones are saved and the sync counts as successful.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_SomeUnreadable_SavedAndSkippedCountRecorded()
	{
		var store = new InMemoryStore { Session = Connected("current", "savings") };
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns(new BankDownload([Debit("tx-1", "current")], Skipped: 2));
		_bank.GetTransactionsAsync("savings", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns(new BankDownload([], Skipped: 1));

		var (added, error) = await Sync(store).SyncAsync();

		error.Should().BeNull();
		added.Should().Be(1);
		store.Session.SkippedTransactions.Should().Be(3);
		store.Session.LastError.Should().BeNull();
	}

	/// <summary>
	/// Verifies that an incremental sync keeps the count of transactions a full sync left out: it only asks for the
	/// last 30 days, so it cannot tell whether those are still missing.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_IncrementalAfterSkipped_CountKept()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		store.Session.RecordFullSync(DateTimeOffset.UtcNow.AddHours(-1), skippedTransactions: 2);
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), false).Returns(Download(Debit("tx-new", "current")));

		var (_, error) = await Sync(store).SyncAsync();

		error.Should().BeNull();
		await _bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), false);
		store.Session.SkippedTransactions.Should().Be(2);
	}

	/// <summary>
	/// Verifies that a sync waiting behind a full sync works on the session the full sync saved, so it does not bring
	/// back the left-out count the full sync just cleared.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_QueuedBehindFullSync_KeepsItsReset()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		store.Session.RecordFullSync(DateTimeOffset.UnixEpoch, skippedTransactions: 3);
		var fullDownload = new TaskCompletionSource<BankDownload>();
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true).Returns(fullDownload.Task);
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), false).Returns(Download());

		var full = Sync(store).SyncAsync(full: true);
		var queued = Sync(store).SyncAsync();
		fullDownload.SetResult(Download());
		await Task.WhenAll(full, queued);

		store.Session.SkippedTransactions.Should().Be(0);
	}

	/// <summary>
	/// Verifies that when reading the session fails, the failure is returned (cut to the length the session keeps)
	/// instead of thrown, and the gate is released, so the next sync still runs.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_SessionReadFails_ReportedAndGateReleased()
	{
		var store = new InMemoryStore { Session = Connected("current"), FailingLoad = 1, LockMessage = new string('x', 400) };
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download(Debit("tx-1", "current")));

		var (_, error) = await Sync(store).SyncAsync();
		var next = Sync(store).SyncAsync();
		var finished = await Task.WhenAny(next, Task.Delay(TimeSpan.FromSeconds(5)));

		error.Should().HaveLength(SessionInfo.MaxErrorLength);
		finished.Should().BeSameAs(next, "the gate must have been released");
		(await next).Error.Should().BeNull();
	}

	/// <summary>
	/// Verifies that when saving the outcome of a successful sync fails, the merged transactions are kept and counted,
	/// the error is returned, the stored session keeps its previous state, and the gate is released.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_OutcomeSaveFails_AddedCountedAndSessionUnchanged()
	{
		var store = new InMemoryStore { Session = Connected("current"), FailingSave = 1 };
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download(Debit("tx-1", "current")));

		var (added, error) = await Sync(store).SyncAsync();
		var storedAfterFailure = store.Session;
		var (_, nextError) = await Sync(store).SyncAsync();

		added.Should().Be(1, "the transactions were merged before the save failed");
		error.Should().Contain("locked");
		storedAfterFailure.LastSync.Should().BeNull("the failed save must not leave a half-recorded sync");
		storedAfterFailure.LastError.Should().BeNull();
		nextError.Should().BeNull("the gate must have been released");
	}

	/// <summary>
	/// Verifies that when the check before saving the outcome (reading the stored session) fails, the merged
	/// transactions are still counted and the error is returned.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_OutcomeSaveCheckFails_AddedCountedAndReported()
	{
		var store = new InMemoryStore { Session = Connected("current"), FailingLoad = 2 };
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(Download(Debit("tx-1", "current")));

		var (added, error) = await Sync(store).SyncAsync();
		var (_, nextError) = await Sync(store).SyncAsync();

		added.Should().Be(1);
		error.Should().Contain("locked");
		nextError.Should().BeNull("the gate must have been released");
	}

	/// <summary>
	/// Verifies that after an incremental sync whose outcome could not be saved, the next sync records the time, and
	/// that a long file error comes back cut to the length the session keeps.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_IncrementalSaveFailsThenSucceeds_ErrorCutThenSyncRecorded()
	{
		var store = new InMemoryStore { Session = SyncedAgo(TimeSpan.FromHours(1), "current"), FailingSave = 1, LockMessage = new string('x', 400) };
		store.Transactions.Add(Debit("tx-old", "current"));
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), false).Returns(Download(Debit("tx-new", "current")));

		var (_, error) = await Sync(store).SyncAsync();
		var (_, nextError) = await Sync(store).SyncAsync();

		error.Should().HaveLength(SessionInfo.MaxErrorLength);
		nextError.Should().BeNull();
		store.Session.LastSync.Should().NotBeNull("the next sync records what the failed save could not");
	}

	/// <summary>
	/// Verifies that when the bank call fails and recording that failure cannot be saved either, the save error is
	/// returned and the gate is released.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_BankAndFailureSaveFail_SaveErrorReturnedAndGateReleased()
	{
		var store = new InMemoryStore { Session = Connected("current"), FailingSave = 1 };
		_bank.GetTransactionsAsync(default!, default, default)
			.ReturnsForAnyArgs<BankDownload>(_ => throw new InvalidOperationException("Enable Banking GET failed (500)"));

		var (added, error) = await Sync(store).SyncAsync();
		var (_, nextError) = await Sync(store).SyncAsync();

		added.Should().Be(0);
		error.Should().Contain("locked");
		nextError.Should().Contain("500", "the second sync runs and records the bank failure");
	}

	/// <summary>
	/// Verifies that a file error from the bank call, such as a missing private key, is recorded in the session, so
	/// the pages show that the sync failed.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_PrivateKeyMissing_ErrorStoredInSession()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		_bank.GetTransactionsAsync(default!, default, default)
			.ReturnsForAnyArgs<BankDownload>(_ => throw new FileNotFoundException("Could not find file 'kbc.pem'."));

		var (_, error) = await Sync(store).SyncAsync();

		error.Should().Contain("kbc.pem");
		store.Session.LastError.Should().Be(error);
	}

	/// <summary>
	/// Verifies that a reconnect with the same session id is still kept: the connection date tells them apart.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_ReconnectedWithSameIdWhileRunning_NewSessionKept()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		var running = new TaskCompletionSource<BankDownload>();
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true).Returns(running.Task);
		var reconnectedAt = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
		var reconnected = SessionInfo.Rehydrate(new BankSessionId("s"), ["current"], reconnectedAt, reconnectedAt.AddDays(180), lastSync: null, lastError: null, skippedTransactions: 0);

		var sync = Sync(store).SyncAsync(full: true);
		store.Session = reconnected;
		running.SetResult(Download());
		await sync;

		store.Session.ConnectedAt.Should().Be(reconnectedAt);
		store.Session.LastSync.Should().BeNull("the old sync's outcome must not overwrite the new connection");
	}

	/// <summary>
	/// Verifies that a sync waiting behind another finds the connection gone and says so, instead of failing.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_SessionGoneWhileQueued_NotConnected()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		var running = new TaskCompletionSource<BankDownload>();
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true).Returns(running.Task);

		var first = Sync(store).SyncAsync(full: true);
		var queued = Sync(store).SyncAsync();
		store.Session = null;
		running.SetResult(Download());
		await first;

		(await queued).Error.Should().Be("Not connected to the bank yet.");
	}

	/// <summary>
	/// Verifies that a reconnect saved while a sync runs is kept: the sync of the old connection does not save its
	/// outcome over the new one.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_ReconnectedWhileRunning_NewSessionKept()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		var running = new TaskCompletionSource<BankDownload>();
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true).Returns(running.Task);
		var reconnected = SessionInfo.Rehydrate(new BankSessionId("new"), ["current"], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(180), lastSync: null, lastError: null, skippedTransactions: 0);

		var sync = Sync(store).SyncAsync(full: true);
		store.Session = reconnected;
		running.SetResult(Download(Debit("tx-new", "current")));
		var (added, _) = await sync;

		added.Should().Be(1, "the downloaded transactions are still saved");
		store.Session.Id.Value.Should().Be("new");
		store.Session.LastSync.Should().BeNull("the old sync's outcome must not overwrite the new connection");
	}

	/// <summary>
	/// Verifies that a bank failure is not thrown but stored in the session, so the user sees it, and that no
	/// transaction is lost.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_BankFails_ErrorStoredInSession()
	{
		var store = new InMemoryStore { Session = Connected("current") };
		store.Transactions.Add(Debit("tx-old", "current"));
		_bank.GetTransactionsAsync(default!, default, default)
			.ReturnsForAnyArgs<BankDownload>(_ => throw new InvalidOperationException("Enable Banking GET failed (401): EXPIRED_SESSION"));

		var (added, error) = await Sync(store).SyncAsync(full: true);

		added.Should().Be(0);
		error.Should().Contain("401");
		store.Session.LastError.Should().Be(error);
		store.Transactions.Should().HaveCount(1, "a failed sync never removes stored transactions");
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : An <see cref="IBankSyncPersistence"/> that keeps the session and the transactions in memory,
	///              merging by id like the real one, so tests can inspect what a sync saved.
	/// </summary>
	private sealed class InMemoryStore : IBankSyncPersistence
	{
		#region Fields
		/// <summary>
		/// The number of <see cref="LoadSession"/> calls so far.
		/// </summary>
		private int _loads;

		/// <summary>
		/// The number of <see cref="SaveSession"/> calls so far.
		/// </summary>
		private int _saves;
		#endregion

		#region Properties
		/// <summary>
		/// Gets or sets the bank session; <see langword="null"/> means not connected.
		/// </summary>
		public SessionInfo? Session { get; set; }

		/// <summary>
		/// Gets the stored transactions.
		/// </summary>
		public List<Txn> Transactions { get; } = [];

		/// <summary>
		/// Gets or sets the number of the <see cref="LoadSession"/> call that fails; 0 for none.
		/// </summary>
		public int FailingLoad { get; set; }

		/// <summary>
		/// Gets or sets the number of the <see cref="SaveSession"/> call that fails; 0 for none.
		/// </summary>
		public int FailingSave { get; set; }

		/// <summary>
		/// Gets or sets the message of the simulated file error.
		/// </summary>
		public string LockMessage { get; set; } = "session.json is locked";
		#endregion

		#region IBankSyncPersistence Implementation
		/// <inheritdoc />
		public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) =>
			Task.FromResult<IAsyncDisposable?>(Substitute.For<IAsyncDisposable>());

		/// <inheritdoc />
		public Task<SessionInfo?> LoadSessionAsync(CancellationToken cancellationToken) => Task.FromResult(LoadSession());

		/// <inheritdoc />
		public Task<bool> HasTransactionsAsync(string accountUid, CancellationToken cancellationToken) =>
			Task.FromResult(Transactions.Exists(t => string.Equals(t.AccountUid, accountUid, StringComparison.Ordinal)));

		/// <inheritdoc />
		public Task<int> MergeAsync(IEnumerable<Txn> incoming, CancellationToken cancellationToken) => Task.FromResult(Merge(incoming));

		/// <inheritdoc />
		public Task<bool> SaveIfCurrentAsync(SessionInfo session, CancellationToken cancellationToken)
		{
			var current = LoadSession();
			if (current is null || current.Id != session.Id || current.ConnectedAt != session.ConnectedAt)
			{
				return Task.FromResult(false);
			}

			SaveSession(session);
			return Task.FromResult(true);
		}
		#endregion

		#region Private Helpers
		/// <summary>Reads a copy of the session, as real storage materialises a new object on every call, and throws an
		/// <see cref="IOException"/> on the call numbered <see cref="FailingLoad"/>, as a storage failure would.</summary>
		private SessionInfo? LoadSession()
		{
			if (++_loads == FailingLoad)
			{
				throw new IOException(LockMessage);
			}

			return Session is null
				? null
				: SessionInfo.Rehydrate(Session.Id, Session.AccountUids, Session.ConnectedAt, Session.ValidUntil, Session.LastSync, Session.LastError, Session.SkippedTransactions);
		}

		/// <summary>Keeps the session, throwing an <see cref="IOException"/> on the call numbered
		/// <see cref="FailingSave"/>, as a storage failure would.</summary>
		private void SaveSession(SessionInfo session)
		{
			if (++_saves == FailingSave)
			{
				throw new IOException(LockMessage);
			}

			Session = session;
		}

		/// <summary>Adds new transactions and replaces known ones by id, never deleting any.</summary>
		private int Merge(IEnumerable<Txn> incoming)
		{
			var added = 0;
			foreach (var txn in incoming)
			{
				var index = Transactions.FindIndex(t => t.Id.Equals(txn.Id));
				if (index >= 0)
				{
					Transactions[index] = txn;
				}
				else
				{
					Transactions.Add(txn);
					added++;
				}
			}

			return added;
		}

		#endregion
	}
	#endregion
}
