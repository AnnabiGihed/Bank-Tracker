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
			user.DismissSuggestion("CARD:NEW");
			user.RemoveFixedPayment(new RecurringPaymentId("rent"));
			user.AcceptFixedPayment(youTube);
		});
		var read = await ReadAsync(db => db.UserData.SingleAsync());

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		read.Payees.Keys.Should().BeEquivalentTo("CARD:SHOP ONE", "CARD:OTHER");
		read.TxnCategories.Should().HaveCount(2);
		read.PayeeNames["CARD:SHOP ONE"].Should().Be("Corner shop");
		read.DismissedSuggestions.Should().BeEquivalentTo("CARD:OLD SHOP", "CARD:NEW", "TO:BE00111122223333");
		read.Recurring.Select(r => r.Id.Value).Should().Equal("youtube");
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
