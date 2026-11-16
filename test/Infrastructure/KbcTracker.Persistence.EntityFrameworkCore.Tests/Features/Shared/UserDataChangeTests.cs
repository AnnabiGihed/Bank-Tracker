namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that changes made through the decisions' behaviours are saved: the JSON columns change in
///              place, so EF Core only sees them through the deep comparers, and a stale copy cannot overwrite a newer
///              save.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
public sealed class UserDataChangeTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Change Tests
	/// <summary>
	/// Verifies that every kind of decision changed on a loaded aggregate reaches the database.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Behaviours_OnLoadedDecisions_Saved()
	{
		await AddAsync(Samples.Decisions());
		var youTube = RecurringPayment.Create(
			new RecurringPaymentId("youtube"), "YouTube", null, new PayeeMatch(["CARD:GOOGLE YOUTUBE"], ["YOUTUBE"]),
			new RecurrenceRule(13.99m, 10m, 30), Samples.BrusselsMorning).Value;

		var result = await SaveAsync(async db =>
		{
			var user = await db.UserData.SingleAsync().ConfigureAwait(false);
			user.AssignPayeeCategory("CARD:OTHER", new CategoryId("cash"));
			user.AssignTransactionCategory(new TxnId("t2"), new CategoryId("bills"));
			user.RenamePayee("CARD:SHOP ONE", "Corner shop");
			user.SplitPayment(new TxnId("t3"), PaymentSplit.Rehydrate([new SplitLine(1m, new CategoryId("cash"), null), new SplitLine(2m, new CategoryId("bills"), null)]));
			user.ReassignSplitLines(new TxnId("t2"), new CategoryId("car"), new CategoryId("cash"));
			user.DismissSuggestion("CARD:NEW");
			user.RemoveFixedPayment(new RecurringPaymentId("rent"));
			user.AcceptFixedPayment(youTube);
		});
		var read = await ReadAsync(db => db.UserData.SingleAsync());

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		read.Payees.Keys.Should().BeEquivalentTo("CARD:SHOP ONE", "CARD:OTHER");
		read.TxnCategories.Should().HaveCount(2);
		read.Splits.Keys.Should().BeEquivalentTo([new TxnId("t2"), new TxnId("t3")]);
		read.Splits[new TxnId("t2")].Lines.Select(l => l.CategoryId.Value).Should().Equal("groceries", "cash");
		read.PayeeNames["CARD:SHOP ONE"].Should().Be("Corner shop");
		read.DismissedSuggestions.Should().BeEquivalentTo("CARD:OLD SHOP", "CARD:NEW", "TO:BE00111122223333");
		read.Recurring.Select(r => r.Id.Value).Should().Equal("youtube");
	}

	/// <summary>
	/// Verifies that adding, correcting and removing debts on a loaded aggregate reaches the database: the debts live in
	/// one JSON column, so only the deep comparer lets EF Core see an in-place change.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task DebtBehaviours_OnLoadedDecisions_Saved()
	{
		await AddAsync(Samples.Decisions());
		var terms = new DebtTerms(DebtKind.PersonalLoan, "Brother", null, null, 400m, null, null, new DateOnly(2026, 10, 1));

		var result = await SaveAsync(async db =>
		{
			var user = await db.UserData.SingleAsync().ConfigureAwait(false);
			user.AddDebt(new DebtId("brother"), terms);
			user.IncludeRepayment(new DebtId("brother"), new TxnId("t5"));
			user.ExcludeRepayment(new DebtId("friend"), new TxnId("t6"));
			user.ResetRepayment(new DebtId("friend"), new TxnId("t4"));
			user.UpdateDebt(new DebtId("friend"), new DebtTerms(DebtKind.PersonalLoan, "Friend renamed", null, null, 900m, null, null, new DateOnly(2026, 10, 1)));
		});
		var read = await ReadAsync(db => db.UserData.SingleAsync());

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		read.Debts.Select(d => d.Id.Value).Should().Equal("friend", "brother");
		read.Debts[0].Lender.Should().Be("Friend renamed");
		read.Debts[0].Plan.Should().BeNull();
		read.Debts[0].IncludedPayments.Should().BeEquivalentTo([new TxnId("t3")]);
		read.Debts[0].ExcludedPayments.Should().BeEquivalentTo([new TxnId("t6")]);
		read.Debts[1].IncludedPayments.Should().BeEquivalentTo([new TxnId("t5")]);

		var removed = await SaveAsync(async db => (await db.UserData.SingleAsync().ConfigureAwait(false)).RemoveDebt(new DebtId("brother")));
		removed.IsSuccess.Should().BeTrue();
		(await ReadAsync(db => db.UserData.SingleAsync())).Debts.Select(d => d.Id.Value).Should().Equal("friend");
	}

	/// <summary>
	/// Verifies that saving decisions loaded before another save fails instead of overwriting it.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Save_StaleDecisions_ConcurrencyFailure()
	{
		await AddAsync(Samples.Decisions());
		await using var staleScope = Services.CreateAsyncScope();
		var staleDb = staleScope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
		var stale = await staleDb.UserData.SingleAsync();

		var first = await SaveAsync(async db => (await db.UserData.SingleAsync().ConfigureAwait(false)).RenamePayee("CARD:SHOP ONE", "First"));
		stale.RenamePayee("CARD:SHOP ONE", "Second");
		var second = await staleScope.ServiceProvider.GetRequiredService<Pivot.Framework.Domain.Repositories.IUnitOfWork>().SaveChangesAsync();

		first.IsSuccess.Should().BeTrue();
		second.IsFailure.Should().BeTrue();
		second.Error.Code.Should().Be("DbUpdateConcurrencyError");
		(await ReadAsync(db => db.UserData.SingleAsync())).PayeeNames["CARD:SHOP ONE"].Should().Be("First");
	}
	#endregion
}
