namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies what the round-trip mapping tests leave out: queries by strongly typed id translate to SQL,
///              changes to categories and bank connections are saved, a stale copy of either cannot overwrite a newer
///              save, and a payee confirmed without a category keeps its empty category.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
public sealed class AggregateChangeTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Lookup Tests
	/// <summary>Verifies each aggregate is found by its strongly typed id in a database query.</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Lookups_ByTypedId_TranslatedAndFound()
	{
		await AddAsync(Samples.Txn("t1"), Samples.Txn("t2"), Samples.Groceries(), Samples.Session());
		var transactionId = new TxnId("t2");
		var categoryId = new CategoryId("groceries");
		var sessionId = new BankSessionId("session-1");

		(await ReadAsync(db => db.Transactions.SingleAsync(t => t.Id == transactionId))).Id.Should().Be(transactionId);
		(await ReadAsync(db => db.Categories.SingleAsync(c => c.Id == categoryId))).Name.Should().Be("Groceries");
		(await ReadAsync(db => db.BankSessions.SingleAsync(s => s.Id == sessionId))).AccountUids.Should().Equal("current", "savings");
		(await ReadAsync(db => db.Transactions.AnyAsync(t => t.Id == new TxnId("missing")))).Should().BeFalse();
	}
	#endregion

	#region Change Tests
	/// <summary>Verifies a category's new name, type, colour and keywords are saved.</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Category_Updated_Saved()
	{
		await AddAsync(Samples.Groceries());

		var result = await SaveAsync(async db => (await db.Categories.SingleAsync().ConfigureAwait(false))
			.Update("Food", CategoryType.Expense, "#123456", ["COLRUYT", "AD "]).IsSuccess.Should().BeTrue());
		var read = await ReadAsync(db => db.Categories.SingleAsync());

		result.IsSuccess.Should().BeTrue();
		read.Name.Should().Be("Food");
		read.Color.Should().Be("#123456");
		read.Keywords.Should().Equal("COLRUYT", "AD ");
	}

	/// <summary>Verifies a sync outcome recorded on a loaded connection is saved.</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Session_SyncRecorded_Saved()
	{
		await AddAsync(Samples.Session());
		var endedAt = Samples.BrusselsMorning.AddHours(2);

		var result = await SaveAsync(async db => (await db.BankSessions.SingleAsync().ConfigureAwait(false)).RecordFullSync(endedAt, 0));
		var read = await ReadAsync(db => db.BankSessions.SingleAsync());

		result.IsSuccess.Should().BeTrue();
		read.LastSync.Should().Be(endedAt);
		read.SkippedTransactions.Should().Be(0);
		read.LastError.Should().BeNull();
	}

	/// <summary>Verifies a payee confirmed without a category comes back without one, not as "Other".</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Payee_ConfirmedWithoutCategory_NullKept()
	{
		await AddAsync(Samples.Decisions());

		var result = await SaveAsync(async db => (await db.UserData.SingleAsync().ConfigureAwait(false)).AssignPayeeCategory("CARD:NO CATEGORY", null));
		var read = await ReadAsync(db => db.UserData.SingleAsync());

		result.IsSuccess.Should().BeTrue();
		read.Payees["CARD:NO CATEGORY"].Should().Be(new PayeeAssignment(null, Confirmed: true));
	}
	#endregion

	#region Concurrency Tests
	/// <summary>Verifies saving a category loaded before another save fails instead of overwriting it.</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Category_StaleCopy_ConcurrencyFailure()
	{
		await AddAsync(Samples.Groceries());
		await using var staleScope = Services.CreateAsyncScope();
		var stale = await staleScope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>().Categories.SingleAsync();

		var first = await SaveAsync(async db => (await db.Categories.SingleAsync().ConfigureAwait(false))
			.Update("First", CategoryType.Expense, "#111111", []).IsSuccess.Should().BeTrue());
		stale.Update("Second", CategoryType.Expense, "#222222", []).IsSuccess.Should().BeTrue();
		var second = await staleScope.ServiceProvider.GetRequiredService<Pivot.Framework.Domain.Repositories.IUnitOfWork>().SaveChangesAsync();

		first.IsSuccess.Should().BeTrue();
		second.IsFailure.Should().BeTrue();
		second.Error.Code.Should().Be("DbUpdateConcurrencyError");
		(await ReadAsync(db => db.Categories.SingleAsync())).Name.Should().Be("First");
	}

	/// <summary>Verifies saving a connection loaded before another save fails instead of overwriting it.</summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Session_StaleCopy_ConcurrencyFailure()
	{
		await AddAsync(Samples.Session());
		await using var staleScope = Services.CreateAsyncScope();
		var stale = await staleScope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>().BankSessions.SingleAsync();

		var first = await SaveAsync(async db => (await db.BankSessions.SingleAsync().ConfigureAwait(false)).RecordFailure("first"));
		stale.RecordFailure("second");
		var second = await staleScope.ServiceProvider.GetRequiredService<Pivot.Framework.Domain.Repositories.IUnitOfWork>().SaveChangesAsync();

		first.IsSuccess.Should().BeTrue();
		second.IsFailure.Should().BeTrue();
		second.Error.Code.Should().Be("DbUpdateConcurrencyError");
		(await ReadAsync(db => db.BankSessions.SingleAsync())).LastError.Should().Be("first");
	}
	#endregion
}
