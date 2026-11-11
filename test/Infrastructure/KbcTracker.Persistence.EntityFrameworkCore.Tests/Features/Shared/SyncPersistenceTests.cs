using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.BankConnection.Services;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using KbcTracker.Domain.Features.Categories.Repositories;
using KbcTracker.Domain.Features.Shared.Repositories;
using KbcTracker.Domain.Features.Transactions.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies repository queries, durable bank-sync batches and cross-process exclusion on PostgreSQL.
/// </summary>
/// <param name="fixture">The shared disposable PostgreSQL instance.</param>
public sealed class SyncPersistenceTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Batch Tests
	/// <summary>Verifies recording a rejected batch cannot accidentally resubmit its tracked changes.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task MergeAsync_DatabaseRejectsBatch_FailureOutcomeDoesNotResubmitTransactions()
	{
		await AddAsync(Samples.Session(), Samples.Txn("existing"));
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
			var session = await persistence.LoadSessionAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The test consent must exist.");
			var rejected = Txn.Rehydrate(new TxnId("rejected"), "acc-1", new DateOnly(2026, 11, 2),
				Money.Rehydrate(-1m, "EUR"), Counterparty.Rehydrate("Shop", null), "Test", new BankRecord("PMNT", "invalid\0text"));
			var merge = async () => await persistence.MergeAsync([Changed("existing", -99m), rejected], CancellationToken.None).ConfigureAwait(false);

			await merge.Should().ThrowAsync<InvalidOperationException>();
			session.RecordFailure("Batch could not be stored.");
			(await persistence.SaveIfCurrentAsync(session, CancellationToken.None)).Should().BeTrue();
		}

		var history = await ReadAsync(db => db.Transactions.ToListAsync());
		history.Should().ContainSingle().Which.Amount.Should().Be(Samples.Txn("existing").Amount);
		(await ReadAsync(db => db.BankSessions.SingleAsync())).LastError.Should().Be("Batch could not be stored.");
	}

	/// <summary>Verifies known transactions update, duplicate ids count once and old history remains intact.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task MergeAsync_DuplicateAndExistingIds_HistoryRetainedAndCountDurable()
	{
		await AddAsync(Samples.Txn("old"), Samples.Txn("updated"));
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
			(await persistence.MergeAsync([Changed("updated", -99m), Changed("new", -5m), Changed("new", -6m)], CancellationToken.None)).Should().Be(1);
			(await persistence.MergeAsync([Changed("new", -6m)], CancellationToken.None)).Should().Be(0);
		}

		var history = await ReadAsync(db => db.Transactions.OrderBy(t => t.Id).ToListAsync());
		history.Select(t => t.Id.Value).Should().Equal("new", "old", "updated");
		history.Single(t => t.Id == new TxnId("new")).Amount.Should().Be(-6m);
		history.Single(t => t.Id == new TxnId("updated")).Amount.Should().Be(-99m);
		history.Single(t => t.Id == new TxnId("old")).Raw.Should().Be(Samples.Txn("old").Raw);
	}

	/// <summary>Verifies staging is isolated until the unit of work commits, and queries page in stable order.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Repositories_StagedBatch_NotVisibleUntilCommitted()
	{
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var transactions = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
			(await transactions.HasAnyAsync("acc-1", CancellationToken.None)).Should().BeFalse();
			(await transactions.StageMergeAsync([Samples.Txn("b"), Samples.Txn("a")], CancellationToken.None)).Should().Be(2);
			(await ReadAsync(db => db.Transactions.CountAsync())).Should().Be(0);
			var saved = await scope.ServiceProvider.GetRequiredService<Pivot.Framework.Domain.Repositories.IUnitOfWork>().SaveChangesAsync();
			saved.IsSuccess.Should().BeTrue();
			(await transactions.HasAnyAsync("acc-1", CancellationToken.None)).Should().BeTrue();
			(await transactions.HasAnyAsync("other-account", CancellationToken.None)).Should().BeFalse();
			(await transactions.ListAsync(1, 1, CancellationToken.None)).Select(t => t.Id.Value).Should().Equal("b");
		}
	}
	#endregion

	#region Outcome Tests
	/// <summary>Verifies saved sync outcomes survive a new request scope.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SaveIfCurrentAsync_CurrentConsent_OutcomePersisted()
	{
		await AddAsync(Samples.Session());
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
			var session = await persistence.LoadSessionAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The test consent must exist.");
			session.RecordFullSync(Samples.BrusselsMorning.AddHours(1), 7);
			(await persistence.SaveIfCurrentAsync(session, CancellationToken.None)).Should().BeTrue();
		}

		var read = await ReadAsync(db => db.BankSessions.SingleAsync());
		read.LastSync.Should().Be(Samples.BrusselsMorning.AddHours(1));
		read.SkippedTransactions.Should().Be(7);
	}

	/// <summary>Verifies a stale sync cannot overwrite either kind of reconnect.</summary>
	/// <param name="sameId">Whether the bank reused the original consent identifier.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SaveIfCurrentAsync_Reconnected_OriginalOutcomeIgnored(bool sameId)
	{
		await AddAsync(Samples.Session());
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
			var stale = await persistence.LoadSessionAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The test consent must exist.");
			var replacement = SessionInfo.Connect(new BankSessionId(sameId ? stale.Id.Value : "replacement"), ["new-account"],
				stale.ConnectedAt.AddHours(1), stale.ValidUntil.AddHours(1), lastSync: null).Value;
			var saved = await SaveAsync(async db =>
			{
				if (sameId)
				{
					var current = await db.BankSessions.SingleAsync().ConfigureAwait(false);
					db.Entry(current).CurrentValues.SetValues(replacement);
				}
				else
				{
					db.BankSessions.Add(replacement);
				}
			});
			saved.IsSuccess.Should().BeTrue();
			stale.RecordFailure("Stale sync failure");
			(await persistence.SaveIfCurrentAsync(stale, CancellationToken.None)).Should().BeFalse();
		}

		var latest = await ReadAsync(db => db.BankSessions.OrderByDescending(s => s.ConnectedAt).FirstAsync());
		latest.LastError.Should().BeNull();
		latest.ConnectedAt.Should().Be(Samples.BrusselsMorning.AddHours(1));
	}
	#endregion

	#region Lease Tests
	/// <summary>Verifies independent backend connections cannot own the sync lease simultaneously.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task TryAcquireAsync_IndependentScopes_ExclusiveUntilReleased()
	{
		var firstScope = Services.CreateAsyncScope();
		await using (firstScope)
		{
			var secondScope = Services.CreateAsyncScope();
			await using (secondScope)
			{
				var first = firstScope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
				var second = secondScope.ServiceProvider.GetRequiredService<IBankSyncPersistence>();
				var lease = await first.TryAcquireAsync(CancellationToken.None)
					?? throw new InvalidOperationException("The first sync must acquire its lease.");
				(await second.TryAcquireAsync(CancellationToken.None)).Should().BeNull();
				await lease.DisposeAsync();
				var reacquired = await second.TryAcquireAsync(CancellationToken.None)
					?? throw new InvalidOperationException("A released lease must be available.");
				await reacquired.DisposeAsync();
			}
		}
	}
	#endregion

	#region Application Tests
	/// <summary>Verifies the shared sync workflow saves through PostgreSQL without touching the JSON folder.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_PostgreSql_BatchAndOutcomeSurviveNewScope()
	{
		await AddAsync(Samples.Session(), Samples.Txn("historical"));
		var bank = Substitute.For<IBankTransactionSource>();
		bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(new BankDownload([Samples.Txn("downloaded")], 3));
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var sync = new SyncService(bank, scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>(),
				new SyncSettings(13), NullLogger<SyncService>.Instance, TimeProvider.System);
			var (added, error) = await sync.SyncAsync();
			added.Should().Be(1);
			error.Should().BeNull();
		}

		(await ReadAsync(db => db.Transactions.CountAsync())).Should().Be(2);
		var latest = await ReadAsync(db => db.BankSessions.SingleAsync());
		latest.LastSync.Should().BeAfter(Samples.BrusselsMorning);
		latest.SkippedTransactions.Should().Be(6, "each of the two accounts reported three skipped transactions");
	}

	/// <summary>Verifies category and decision queries return the centrally stored data.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadRepositories_ImportedAggregates_ReturnStoredValues()
	{
		await AddAsync(Samples.Groceries(), Samples.Decisions(), Samples.Session());
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>().ListAsync(CancellationToken.None);
			categories.Should().ContainSingle().Which.Name.Should().Be("Groceries");
			var user = await scope.ServiceProvider.GetRequiredService<IUserDataRepository>().GetCurrentAsync(CancellationToken.None);
			user.Should().NotBeNull();
			user.Recurring.Should().ContainSingle();
			var session = await scope.ServiceProvider.GetRequiredService<IBankSessionRepository>().GetCurrentAsync(CancellationToken.None);
			session.Should().NotBeNull();
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds a revised bank transaction without changing its stable identity.</summary>
	private static Txn Changed(string id, decimal amount) => Txn.Rehydrate(new TxnId(id), "acc-1", new DateOnly(2026, 11, 2),
		Money.Rehydrate(amount, "EUR"), Counterparty.Rehydrate("Shop", null), "Updated", new BankRecord("PMNT", "{}"));
	#endregion
}
