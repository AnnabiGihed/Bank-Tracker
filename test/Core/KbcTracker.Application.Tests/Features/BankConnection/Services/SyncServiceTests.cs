using Microsoft.Extensions.Logging.Abstractions;

namespace KbcTracker.Application.Tests.Features.BankConnection.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that <see cref="SyncService"/> downloads every connected account, merges the
///              transactions, records the outcome in the session, and falls back to the longest history when the
///              bank refuses an incremental request.
///
///              The bank is an NSubstitute double of <see cref="IBankTransactionSource"/>; the store is an in-memory
///              <see cref="IStore"/> that merges by id like the real ones.
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
		new(_bank, store, new SyncSettings(HistoryMonths: 13), NullLogger<SyncService>.Instance);

	/// <summary>
	/// A booked debit of 10 € in the given account.
	/// </summary>
	private static Txn Debit(string id, string accountUid) =>
		new(id, accountUid, new DateOnly(2026, 11, 2), -10m, "EUR", "Shop", "Test");
	#endregion

	#region Sync Tests
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
		var store = new InMemoryStore { Session = new SessionInfo { SessionId = "s", AccountUids = ["current", "savings"] } };
		_bank.GetTransactionsAsync("current", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns([Debit("tx-current", "current")]);
		_bank.GetTransactionsAsync("savings", Arg.Any<DateOnly?>(), Arg.Any<bool>()).Returns([Debit("tx-savings", "savings")]);

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
		var store = new InMemoryStore { Session = new SessionInfo { AccountUids = ["current"] } };
		_bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs([Debit("tx-1", "current")]);

		await Sync(store).SyncAsync();

		await _bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true);
	}

	/// <summary>
	/// Verifies that when the bank refuses an incremental request, the sync retries once with the longest history
	/// and still succeeds.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_IncrementalRefused_RetriesWithLongest()
	{
		var store = new InMemoryStore { Session = new SessionInfo { AccountUids = ["current"] } };
		store.Merge([Debit("tx-old", "current")]);
		_bank.GetTransactionsAsync("current", Arg.Is<DateOnly?>(d => d != null), false)
			.Returns<IReadOnlyList<Txn>>(_ => throw new InvalidOperationException("WRONG_TRANSACTIONS_PERIOD"));
		_bank.GetTransactionsAsync("current", null, true).Returns([Debit("tx-new", "current")]);

		var (added, error) = await Sync(store).SyncAsync();

		error.Should().BeNull();
		added.Should().Be(1);
		await _bank.Received(1).GetTransactionsAsync("current", null, true);
	}

	/// <summary>
	/// Verifies that a bank failure is not thrown but stored in the session, so the user sees it, and that no
	/// transaction is lost.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task SyncAsync_BankFails_ErrorStoredInSession()
	{
		var store = new InMemoryStore { Session = new SessionInfo { AccountUids = ["current"] } };
		store.Merge([Debit("tx-old", "current")]);
		_bank.GetTransactionsAsync(default!, default, default)
			.ReturnsForAnyArgs<IReadOnlyList<Txn>>(_ => throw new InvalidOperationException("Enable Banking GET failed (401): EXPIRED_SESSION"));

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
	/// Purpose     : An <see cref="IStore"/> that keeps the session and the transactions in memory, merging by id
	///              like the real stores, so tests can inspect what a sync saved.
	/// </summary>
	private sealed class InMemoryStore : IStore
	{
		#region Properties
		/// <summary>
		/// Gets or sets the bank session; <see langword="null"/> means not connected.
		/// </summary>
		public SessionInfo? Session { get; set; }

		/// <summary>
		/// Gets the stored transactions.
		/// </summary>
		public List<Txn> Transactions { get; } = [];
		#endregion

		#region IStore Implementation
		/// <inheritdoc />
		public SessionInfo? LoadSession() => Session;

		/// <inheritdoc />
		public void SaveSession(SessionInfo session) => Session = session;

		/// <inheritdoc />
		public IReadOnlyList<Txn> LoadTransactions() => [.. Transactions];

		/// <inheritdoc />
		public int Merge(IEnumerable<Txn> incoming)
		{
			var added = 0;
			foreach (var txn in incoming)
			{
				var index = Transactions.FindIndex(t => string.Equals(t.Id, txn.Id, StringComparison.Ordinal));
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

		/// <inheritdoc />
		public UserData LoadUserData() => new();

		/// <inheritdoc />
		public void SaveUserData(UserData data)
		{
		}

		/// <inheritdoc />
		public IList<Category> LoadCategories(IReadOnlyDictionary<string, string[]> seedKeywords) => CategoryDefaults.Create(seedKeywords);

		/// <inheritdoc />
		public void SaveCategories(IEnumerable<Category> categories)
		{
		}
		#endregion
	}
	#endregion
}
