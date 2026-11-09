using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace KbcTracker.Web.Tests.Features.Shared.Storage;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the web host's JSON <see cref="Store"/> in a temporary data folder: transactions are
///              merged by id and never deleted, a daily backup is kept, first-run categories are created, and the
///              user's decisions survive a save and reload unchanged.
/// </summary>
public sealed class StoreTests : IDisposable
{
	#region Fields
	/// <summary>
	/// The temporary data folder of this test.
	/// </summary>
	private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "kbc-store-" + Guid.NewGuid().ToString("N"));

	/// <summary>
	/// The store under test.
	/// </summary>
	private readonly Store _store;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="StoreTests"/> with a store over an empty temporary data folder.
	/// </summary>
	public StoreTests()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["DataDirectory"] = _dataDir })
			.Build();
		_store = new Store(config, NullLogger<Store>.Instance);
	}
	#endregion

	#region Transaction Tests
	/// <summary>
	/// Verifies that merging adds new transactions, updates known ones by id, and reports only the new ones.
	/// </summary>
	[Fact]
	public void Merge_NewAndKnownIds_AddsNewAndUpdatesKnown()
	{
		_store.Merge([Txn("a", -10m), Txn("b", -20m)]);

		var added = _store.Merge([Txn("b", -25m), Txn("c", -30m)]);

		added.Should().Be(1);
		_store.LoadTransactions().Should().HaveCount(3);
		_store.LoadTransactions().Single(t => string.Equals(t.Id, "b", StringComparison.Ordinal)).Amount.Should().Be(-25m);
	}

	/// <summary>
	/// Verifies that a later sync that no longer returns old transactions never removes them.
	/// </summary>
	[Fact]
	public void Merge_LaterSyncWithoutOldTransactions_KeepsThem()
	{
		_store.Merge([Txn("old", -10m)]);

		_store.Merge([Txn("new", -20m)]);

		_store.LoadTransactions().Select(t => t.Id).Should().BeEquivalentTo("old", "new");
	}

	/// <summary>
	/// Verifies that changing an existing history first copies it to today's backup.
	/// </summary>
	[Fact]
	public void Merge_ExistingHistory_DailyBackupKept()
	{
		_store.Merge([Txn("a", -10m)]);

		_store.Merge([Txn("b", -20m)]);

		Directory.GetFiles(Path.Combine(_dataDir, "backups"), "transactions-*.json").Should().HaveCount(1);
	}
	#endregion

	#region Decision Tests
	/// <summary>
	/// Verifies that the first load of categories creates them from the seed and saves them, so the next load reads
	/// the file.
	/// </summary>
	[Fact]
	public void LoadCategories_FirstRun_CreatedFromSeedAndSaved()
	{
		var seed = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["Groceries"] = ["DELHAIZE"] };

		var categories = _store.LoadCategories(seed);

		categories.Should().Contain(c => c.Id == "groceries");
		File.Exists(Path.Combine(_dataDir, "categories.json")).Should().BeTrue();
		_store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal)).Should().Contain(c => c.Id == "groceries");
	}

	/// <summary>
	/// Verifies that every kind of user decision survives a save and a reload: fixed payments, dismissed
	/// suggestions, payee and transaction categories, and payee names.
	/// </summary>
	[Fact]
	public void SaveUserData_AllDecisions_RoundTripUnchanged()
	{
		var user = new UserData();
		user.Recurring.Add(new RecurringPayment { Name = "Rent", Amount = 940m, SeriesKeys = ["TO:BE00111122223333"] });
		user.DismissedSuggestions.Add("CARD:SHOP ONE");
		user.Payees["CARD:SHOP ONE"] = new PayeeAssignment { CategoryId = "groceries", Confirmed = true };
		user.TxnCategories["tx-1"] = "car";
		user.PayeeNames["CARD:SHOP ONE"] = "Shop One";

		_store.SaveUserData(user);
		var loaded = _store.LoadUserData();

		loaded.Should().BeEquivalentTo(user);
	}

	/// <summary>
	/// Verifies that a corrupt decisions file reads as empty instead of stopping the app.
	/// </summary>
	[Fact]
	public void LoadUserData_CorruptFile_Empty()
	{
		File.WriteAllText(Path.Combine(_dataDir, "userdata.json"), "{ not json");

		_store.LoadUserData().Recurring.Should().BeEmpty();
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => Directory.Delete(_dataDir, recursive: true);
	#endregion

	#region Private Helpers
	/// <summary>
	/// A transaction with the given id and amount.
	/// </summary>
	private static Txn Txn(string id, decimal amount) =>
		new(id, "acc-1", new DateOnly(2026, 11, 2), amount, "EUR", "Shop", "Test");
	#endregion
}
