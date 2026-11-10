using KbcTracker.Infrastructure.Features.BankConnection;
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
	#region Constants
	/// <summary>
	/// A decisions file as the previous version wrote it.
	/// </summary>
	private const string UserDataJson = """
		{
		  "Recurring": [
		    {
		      "Id": "25181db982",
		      "Name": "Rent",
		      "CategoryId": "housing",
		      "SeriesKeys": [
		        "TO:BE00111122223333"
		      ],
		      "Tokens": [],
		      "Amount": 940.00,
		      "TolerancePct": 15,
		      "CadenceDays": 30,
		      "Active": true,
		      "CreatedAt": "2026-11-09T15:23:37.8304434+00:00"
		    }
		  ],
		  "DismissedSuggestions": [
		    "CARD:SHOP ONE"
		  ],
		  "Payees": {
		    "CARD:SHOP ONE": {
		      "CategoryId": "groceries",
		      "Confirmed": true
		    }
		  },
		  "TxnCategories": {
		    "tx-1": "car"
		  },
		  "PayeeNames": {
		    "CARD:SHOP ONE": "Shop One"
		  }
		}
		""";

	/// <summary>
	/// A bank session file as the previous version wrote it.
	/// </summary>
	private const string SessionJson = """
		{
		  "SessionId": "session-1",
		  "AccountUids": [
		    "account-1"
		  ],
		  "ConnectedAt": "2026-11-09T15:09:56.2139245+00:00",
		  "ValidUntil": "2027-05-08T15:09:56.2139247+00:00",
		  "LastSync": "2026-11-09T15:10:12.9764483+00:00",
		  "LastError": null
		}
		""";

	/// <summary>
	/// A categories file as the previous version wrote it; the trailing space of "AD " is meaningful.
	/// </summary>
	private const string CategoriesJson = """
		[
		  {
		    "Id": "groceries",
		    "Name": "Groceries",
		    "Type": "Expense",
		    "Color": "#3454d1",
		    "Keywords": [
		      "AD ",
		      "DELHAIZE"
		    ],
		    "BuiltIn": false
		  }
		]
		""";

	/// <summary>
	/// A transactions file as the previous version wrote it.
	/// </summary>
	private const string TransactionsJson = """
		[
		  {
		    "Id": "tx-1",
		    "AccountUid": "account-1",
		    "Date": "2026-11-02",
		    "Amount": -940.00,
		    "Currency": "EUR",
		    "Counterparty": "LANDLORD",
		    "Description": "Rent November",
		    "CounterpartyIban": "BE00111122223333",
		    "BankCode": "PMNT",
		    "Raw": "bank-json"
		  }
		]
		""";
	#endregion

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
		_store.LoadTransactions().Single(t => string.Equals(t.Id.Value, "b", StringComparison.Ordinal)).Amount.Should().Be(-25m);
	}

	/// <summary>
	/// Verifies that a later sync that no longer returns old transactions never removes them.
	/// </summary>
	[Fact]
	public void Merge_LaterSyncWithoutOldTransactions_KeepsThem()
	{
		_store.Merge([Txn("old", -10m)]);

		_store.Merge([Txn("new", -20m)]);

		_store.LoadTransactions().Select(t => t.Id.Value).Should().BeEquivalentTo("old", "new");
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

		categories.Should().Contain(c => c.Id.Value == "groceries");
		File.Exists(Path.Combine(_dataDir, "categories.json")).Should().BeTrue();
		_store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal)).Should().Contain(c => c.Id.Value == "groceries");
	}

	/// <summary>
	/// Verifies that every kind of user decision survives a save and a reload: fixed payments, dismissed
	/// suggestions, payee and transaction categories, and payee names.
	/// </summary>
	[Fact]
	public void SaveUserData_AllDecisions_RoundTripUnchanged()
	{
		var rent = RecurringPayment.Create(
			new RecurringPaymentId("rent"),
			"Rent",
			new CategoryId("housing"),
			new PayeeMatch(["TO:BE00111122223333"], []),
			new RecurrenceRule(940m, 15m, 30),
			new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.Zero)).Value;
		var user = UserData.Empty();
		user.AcceptFixedPayment(rent);
		user.DismissSuggestion("CARD:SHOP ONE");
		user.AssignPayeeCategory("CARD:SHOP ONE", new CategoryId("groceries"));
		user.AssignTransactionCategory(new TxnId("tx-1"), new CategoryId("car"));
		user.RenamePayee("CARD:SHOP ONE", "Shop One");

		_store.SaveUserData(user);
		var loaded = _store.LoadUserData();

		loaded.Should().BeEquivalentTo(user);
	}

	/// <summary>
	/// Verifies that values older versions could store but the domain no longer accepts never make a file
	/// unreadable: a blank transaction id gets a stable derived id, a category without id is left out, a blank
	/// category reference keeps its old meaning ("Other"), and missing text reads as empty.
	/// </summary>
	[Fact]
	public void Load_LegacyBlankAndNullValues_ReadableWithOldMeaning()
	{
		File.WriteAllText(Path.Combine(_dataDir, "transactions.json"),
			"""[{"Id":"","AccountUid":"acc-1","Date":"2026-11-02","Amount":-5,"Currency":"EUR","Counterparty":null,"Description":null}]""");
		File.WriteAllText(Path.Combine(_dataDir, "categories.json"),
			"""[{"Id":"","Name":"&&"},{"Id":"other","Name":"Other","Color":null,"Keywords":null}]""");
		File.WriteAllText(Path.Combine(_dataDir, "userdata.json"),
			"""{"Recurring":[{"Id":"","Name":"Rent","CategoryId":"","SeriesKeys":["TO:X"],"Amount":940}],"Payees":{"CARD:A":{"CategoryId":"","Confirmed":true},"CARD:B":null},"TxnCategories":{"t1":null,"":"car"},"DismissedSuggestions":null}""");

		var txns = _store.LoadTransactions();
		var categories = _store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal));
		var user = _store.LoadUserData();

		txns.Single().Id.Value.Should().StartWith("legacy-");
		_store.LoadTransactions().Single().Id.Should().Be(txns.Single().Id, "The derived id is stable.");
		categories.Select(c => c.Id.Value).Should().Equal("other");
		var rent = user.Recurring.Single();
		rent.Id.Value.Should().StartWith("legacy-");
		rent.CategoryId.Should().Be(CategoryId.Other);
		user.Payees.Keys.Should().Equal("CARD:A");
		user.Payees["CARD:A"].CategoryId.Should().Be(CategoryId.Other);
		user.TxnCategories.Should().ContainSingle().Which.Value.Should().Be(CategoryId.Other);
	}

	/// <summary>
	/// Verifies that a transaction an older version stored with a blank id gets the id a sync gives its bank JSON,
	/// so syncing it again updates it instead of adding a copy.
	/// </summary>
	[Fact]
	public void Merge_LegacyBlankIdThenSameBankRecord_NoDuplicate()
	{
		File.WriteAllText(Path.Combine(_dataDir, "transactions.json"),
			"""[{"Id":"","AccountUid":"acc-1","Date":"2026-11-02","Amount":-5,"Currency":"EUR","Counterparty":"Shop","Description":"Coffee","Raw":"{\"entry_reference\":\"\",\"transaction_id\":\"tid-9\"}"}]""");
		var synced = Domain.Features.Transactions.Aggregates.Txn.Rehydrate(new TxnId("tid-9"), "acc-1", new DateOnly(2026, 11, 2), Money.Rehydrate(-5m, "EUR"),
			Counterparty.Rehydrate("Shop", null), "Coffee", BankRecord.None);

		var added = _store.Merge([synced]);

		added.Should().Be(0);
		_store.LoadTransactions().Select(t => t.Id.Value).Should().Equal("tid-9");
	}

	/// <summary>
	/// Verifies the other ways a blank stored id is rebuilt: bank JSON without any reference gives the content id a
	/// sync gives the same JSON, and bank JSON that is not an object falls back to an id derived from the content
	/// instead of making the history unreadable.
	/// </summary>
	/// <param name="raw">The stored bank JSON, as a JSON string value.</param>
	/// <param name="expectedPrefix">How the rebuilt id starts.</param>
	[Theory]
	[InlineData("[1]", "legacy-")]
	[InlineData("not json", "legacy-")]
	[InlineData("{}", "")]
	public void Load_LegacyBlankIdWithUnusualRaw_Readable(string raw, string expectedPrefix)
	{
		var stored = new[] { new { Id = "", AccountUid = "acc-1", Date = "2026-11-02", Amount = -5m, Currency = "EUR", Counterparty = "Shop", Description = "Coffee", Raw = raw } };
		File.WriteAllText(Path.Combine(_dataDir, "transactions.json"), System.Text.Json.JsonSerializer.Serialize(stored));

		var id = _store.LoadTransactions().Single().Id.Value;

		id.Should().StartWith(expectedPrefix);
		if (expectedPrefix.Length == 0)
		{
			id.Should().Be(EnableBankingMapper.IdOf("acc-1", System.Text.Json.Nodes.JsonNode.Parse(raw)!), "A re-sync of the same JSON must find it.");
		}
	}

	/// <summary>
	/// Verifies that the number of transactions left out at the last sync survives a save and reload, and that a
	/// session without any keeps the file shape older versions wrote.
	/// </summary>
	[Fact]
	public void SaveSession_SkippedTransactions_RoundTrip()
	{
		_store.SaveSession(_store.LoadSession() ?? SessionInfo.Connect(new BankSessionId("s"), ["acc-1"], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), null).Value);
		var session = _store.LoadSession()!;
		var withoutSkipped = File.ReadAllText(Path.Combine(_dataDir, "session.json"));
		session.RecordFullSync(DateTimeOffset.UnixEpoch.AddHours(1), skippedTransactions: 4);

		_store.SaveSession(session);

		withoutSkipped.Should().NotContain("SkippedTransactions");
		_store.LoadSession()!.SkippedTransactions.Should().Be(4);
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

	/// <summary>
	/// Verifies that files written by the previous version, where the domain types were serialised directly, load
	/// and save back byte for byte: the storage documents keep the files' shape.
	/// </summary>
	/// <param name="fileName">The data file.</param>
	/// <param name="json">Its content in the previous version's shape.</param>
	[Theory]
	[InlineData("userdata.json", UserDataJson)]
	[InlineData("session.json", SessionJson)]
	[InlineData("categories.json", CategoriesJson)]
	[InlineData("transactions.json", TransactionsJson)]
	public void Save_FileFromPreviousVersion_WrittenBackUnchanged(string fileName, string json)
	{
		var path = Path.Combine(_dataDir, fileName);
		File.WriteAllText(path, json);

		switch (fileName)
		{
			case "userdata.json":
				_store.SaveUserData(_store.LoadUserData());
				break;
			case "session.json":
				_store.SaveSession(_store.LoadSession()!);
				break;
			case "categories.json":
				_store.SaveCategories(_store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal)));
				break;
			default:
				_store.Merge([]);
				break;
		}

		File.ReadAllText(path).ReplaceLineEndings().Should().Be(json.ReplaceLineEndings());
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
		Domain.Features.Transactions.Aggregates.Txn.Rehydrate(new TxnId(id), "acc-1", new DateOnly(2026, 11, 2), Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate("Shop", null), "Test", BankRecord.None);
	#endregion
}
