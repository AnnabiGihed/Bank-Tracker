using KbcTracker.Domain.Features.BankConnection.ValueObjects;

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
		read.Accounts.Should().Equal(
			new OwnAccount("current", "BE00111122223333", "Current account", "CACC"),
			new OwnAccount("savings", "BE00444455556666", null, null));
	}

	/// <summary>
	/// Verifies that a connection stored without account details, as before they were kept, reads with none.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Session_WithoutAccountDetails_ReadsEmptyList()
	{
		await AddAsync(SessionInfo.Rehydrate(new BankSessionId("old-session"), ["current"], Samples.BrusselsMorning, Samples.BrusselsMorning.AddDays(180), null, null, 0));

		var read = await ReadAsync(db => db.BankSessions.SingleAsync());

		read.AccountUids.Should().Equal("current");
		read.Accounts.Should().BeEmpty();
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
		read.Splits[new TxnId("t2")].Lines.Should().Equal(
			new SplitLine(50m, new CategoryId("groceries"), "Meat"),
			new SplitLine(34.30m, new CategoryId("car"), null));
	}

	/// <summary>
	/// Verifies that a debt keeps everything the owner said: the lender, the received payment, the plan with its derived
	/// values, and the payments added and excluded by hand.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task UserData_DebtSavedAndRead_EverythingKept()
	{
		await AddAsync(Samples.Decisions());

		var read = await ReadAsync(db => db.UserData.SingleAsync());

		var debt = read.Debts.Should().ContainSingle().Subject;
		debt.Id.Should().Be(new DebtId("friend"));
		debt.Kind.Should().Be(DebtKind.PersonalLoan);
		debt.Lender.Should().Be("Friend");
		debt.Iban.Should().Be("BE00111122223333");
		debt.ReceivedPaymentId.Should().Be(new TxnId("t9"));
		debt.Principal.Should().Be(1000.00m);
		debt.SeriesKey.Should().BeNull();
		debt.IncludedPayments.Should().BeEquivalentTo([new TxnId("t3")]);
		debt.ExcludedPayments.Should().BeEquivalentTo([new TxnId("t4")]);
		var plan = debt.Plan!;
		plan.Amount.Should().Be(1000.00m);
		plan.Start.Should().Be(new DateOnly(2026, 12, 31));
		plan.Frequency.Should().Be(RepaymentFrequency.Monthly);
		plan.DayOfMonth.Should().Be(31);
		plan.InstalmentAmount.Should().Be(300.00m);
		plan.Count.Should().Be(4);
		plan.LastInstalmentAmount.Should().Be(100.00m);
	}

	/// <summary>
	/// Verifies that a debt without a plan, and a single payment plan, are stored and read back.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task UserData_DebtWithoutPlanAndOnePayment_Kept()
	{
		var onePayment = RepaymentPlan.OnePayment(250m, new DateOnly(2027, 3, 1)).Value;
		var noPlan = new DebtTerms(DebtKind.BankLoan, "KBC", null, null, 5000m, "LOAN:726-1", null, new DateOnly(2026, 9, 1));
		var single = new DebtTerms(DebtKind.PersonalLoan, "Sister", null, null, 250m, null, onePayment, new DateOnly(2026, 9, 2));
		await AddAsync(UserData.Rehydrate([], [], [], [], [], [], [Debt.Rehydrate(new DebtId("bank"), noPlan, [], []), Debt.Rehydrate(new DebtId("sister"), single, [], [])]));

		var read = await ReadAsync(db => db.UserData.SingleAsync());

		read.Debts.Select(d => d.Id.Value).Should().Equal("bank", "sister");
		read.Debts[0].Plan.Should().BeNull();
		read.Debts[0].SeriesKey.Should().Be("LOAN:726-1");
		read.Debts[1].Plan!.IsOnePayment.Should().BeTrue();
		read.Debts[1].Plan!.Schedule().Should().Equal(new ScheduledPayment(new DateOnly(2027, 3, 1), 250m));
	}

	/// <summary>
	/// Verifies that a debt stored before the day borrowed existed (version 1) reads with the plan's first date as that day, and
	/// without a plan with the earliest valid date, and that the current version stores and reads the day.
	/// </summary>
	[Fact]
	public void DebtsColumn_VersionOneWithoutBorrowedOn_DefaultsToThePlanStart()
	{
		const string stored = """
			[{"Version":1,"Id":"old","Kind":"PersonalLoan","Lender":"Sister","Iban":null,"ReceivedPaymentId":null,"Principal":250,"SeriesKey":null,
			"Plan":{"Amount":250,"Start":"2027-03-01","Frequency":null,"DayOfMonth":null,"InstalmentAmount":250,"Count":1},"Included":[],"Excluded":[]},
			{"Version":1,"Id":"noplan","Kind":"BankLoan","Lender":"KBC","Iban":null,"ReceivedPaymentId":null,"Principal":900,"SeriesKey":null,"Plan":null,"Included":[],"Excluded":[]}]
			""";
		var converter = Persistence.EntityFrameworkCore.Features.Shared.Conversions.JsonColumns.DebtsConverter;

		var read = (List<Debt>)converter.ConvertFromProvider(stored)!;
		var again = (List<Debt>)converter.ConvertFromProvider((string)converter.ConvertToProvider(read)!)!;

		read[0].BorrowedOn.Should().Be(new DateOnly(2027, 3, 1));
		read[1].BorrowedOn.Should().Be(new DateOnly(RepaymentPlan.MinYear, 1, 1));
		again.Select(d => d.BorrowedOn).Should().Equal(read.Select(d => d.BorrowedOn));
	}
	#endregion
}
