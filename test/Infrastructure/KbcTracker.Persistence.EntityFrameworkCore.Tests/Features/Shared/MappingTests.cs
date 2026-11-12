namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that every aggregate comes back from PostgreSQL exactly as it was saved: ids, exact
///              amounts, the bank's raw JSON as sent, keywords with their spaces, UTC times, and the user's decisions
///              with their owned fixed payments.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
public sealed class MappingTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Round Trip Tests
	/// <summary>
	/// Verifies that a transaction keeps every value, including the amount's scale and the raw JSON byte for byte.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Transaction_SavedAndRead_Unchanged()
	{
		var saved = Samples.Txn("t1");
		await AddAsync(saved);

		var read = await ReadAsync(db => db.Transactions.SingleAsync());

		read.Should().BeEquivalentTo(saved, o => o.Excluding(t => t.Version));
		read.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("-12.50", "the scale must survive");
		read.Raw.Should().Be(saved.Raw, "the analysis reads the raw JSON as the bank sent it");
	}

	/// <summary>
	/// Verifies that a category keeps its keywords in order and with their exact text.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Category_SavedAndRead_KeywordsExact()
	{
		await AddAsync(Samples.Groceries());

		var read = await ReadAsync(db => db.Categories.SingleAsync());

		read.Id.Should().Be(new CategoryId("groceries"));
		read.Type.Should().Be(CategoryType.Expense);
		read.Keywords.Should().Equal("AD ", "DELHAIZE");
	}

	/// <summary>
	/// Verifies that a category keeps its parent and COICOP code, and that a top-level category has neither stored.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Category_ParentAndCoicop_RoundTrip()
	{
		await AddAsync(
			Category.Rehydrate(new CategoryId("transport"), "Transport", CategoryType.Expense, "#3454d1", [], builtIn: false, coicop: "07"),
			Category.Rehydrate(new CategoryId("car"), "Car", CategoryType.Expense, "#3454d1", [], builtIn: false, new CategoryId("transport"), "07.2"));

		var read = await ReadAsync(db => db.Categories.OrderBy(c => c.Id).ToListAsync());

		read.Single(c => c.Id == new CategoryId("car")).ParentId.Should().Be(new CategoryId("transport"));
		read.Single(c => c.Id == new CategoryId("car")).Coicop.Should().Be("07.2");
		read.Single(c => c.Id == new CategoryId("transport")).ParentId.Should().BeNull();
		read.Single(c => c.Id == new CategoryId("transport")).Coicop.Should().Be("07");
	}

	/// <summary>
	/// Verifies that a bank connection keeps its accounts and the same instants, stored in UTC.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Session_SavedAndRead_SameInstantsAndAccounts()
	{
		await AddAsync(Samples.Session());

		var read = await ReadAsync(db => db.BankSessions.SingleAsync());

		read.AccountUids.Should().Equal("current", "savings");
		read.ConnectedAt.Should().Be(Samples.BrusselsMorning, "the instant is the same");
		read.ConnectedAt.Offset.Should().Be(TimeSpan.Zero, "times are stored in UTC");
		read.SkippedTransactions.Should().Be(2);
	}

	/// <summary>
	/// Verifies that the user's decisions keep their fixed payments and every kind of choice.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task UserData_SavedAndRead_AllDecisionsKept()
	{
		await AddAsync(Samples.Decisions());

		var read = await ReadAsync(db => db.UserData.SingleAsync());

		var rent = read.Recurring.Should().ContainSingle().Subject;
		rent.Id.Should().Be(new RecurringPaymentId("rent"));
		rent.CategoryId.Should().Be(new CategoryId("housing"));
		rent.SeriesKeys.Should().Equal("TO:BE00111122223333");
		rent.Tokens.Should().Equal("LANDLORD");
		rent.Amount.Should().Be(940m);
		read.DismissedSuggestions.Should().BeEquivalentTo("CARD:OLD SHOP");
		read.Payees["CARD:SHOP ONE"].Should().Be(new PayeeAssignment(new CategoryId("groceries"), Confirmed: true));
		read.TxnCategories[new TxnId("t1")].Should().Be(new CategoryId("car"));
		read.PayeeNames["CARD:SHOP ONE"].Should().Be("Shop One");
	}
	#endregion
}
