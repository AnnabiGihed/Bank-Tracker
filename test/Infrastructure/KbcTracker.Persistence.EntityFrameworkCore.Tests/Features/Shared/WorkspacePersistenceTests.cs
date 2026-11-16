using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Domain.Features.Categories.Services;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies first-run workspace reads stay read-only, uninitialised databases stage nothing and initial
///              decisions commit defaults atomically.
/// </summary>
/// <param name="fixture">The real PostgreSQL test server.</param>
public sealed class WorkspacePersistenceTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Initialisation Tests
	/// <summary>Verifies a database that was neither imported nor initialised renders built-ins without writing and
	/// refuses to stage anything for a decision, so the one-time import stays possible.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_NotInitialised_ReadsBuiltInsAndStagesNothing()
	{
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
			var read = await persistence.ReadAsync(CancellationToken.None);
			read.Categories.Select(c => c.Id.Value).Should().Contain(["other", "bills", "cash"]);
			var lease = await persistence.TryAcquireWriteAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The first writer must acquire the lease.");
			await using (lease)
			{
				(await persistence.LoadForUpdateAsync(CancellationToken.None)).Should().BeNull();
				(await persistence.SaveAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
			}
		}

		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(0);
		(await ReadAsync(db => db.UserData.CountAsync())).Should().Be(0);
	}

	/// <summary>Verifies the workspace a decision loads carries the bank connection's accounts, so its analysis recognises own
	/// transfers like the one the owner reads.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_LoadForUpdate_CarriesTheConnectionAccounts()
	{
		await AddAsync(UserData.Empty(), Samples.Session());
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
			var lease = await persistence.TryAcquireWriteAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The first writer must acquire the lease.");
			await using (lease)
			{
				var data = await persistence.LoadForUpdateAsync(CancellationToken.None)
					?? throw new InvalidOperationException("An initialised database must load.");

				data.Session.Should().NotBeNull();
				data.Session.Accounts.Select(a => a.Iban).Should().Equal("BE00111122223333", "BE00444455556666");
			}
		}
	}

	/// <summary>Verifies an initialised database without categories gets the default category tree in the same save as the first
	/// decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_InitialisedWithoutCategories_FirstDecisionAddsDefaultTree()
	{
		await AddAsync(UserData.Empty());
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
			var lease = await persistence.TryAcquireWriteAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The first writer must acquire the lease.");
			await using (lease)
			{
				var data = await persistence.LoadForUpdateAsync(CancellationToken.None)
					?? throw new InvalidOperationException("An initialised database must load.");
				data.User.DismissSuggestion("dismissed-in-first-decision");
				(await persistence.SaveAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
			}
		}

		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(CategoryDefaults.CreateDefaults("BE").Count);
		var stored = await ReadAsync(db => db.UserData.SingleAsync());
		stored.DismissedSuggestions.Should().Contain("dismissed-in-first-decision");
	}

	/// <summary>Verifies a database stored before the built-in <c>loan-received</c> category existed gets it with the next
	/// decision, and shows it on a read, without touching the other categories.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_OldTreeWithoutLoanReceived_ReadShowsItAndDecisionAddsIt()
	{
		var old = CategoryDefaults.CreateDefaults("BE")
			.Where(c => c.Id != CategoryId.LoanReceived).ToList();
		await AddAsync([UserData.Empty(), .. old]);
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
			var read = await persistence.ReadAsync(CancellationToken.None);
			read.Categories.Should().Contain(c => c.Id == CategoryId.LoanReceived && c.Type == CategoryType.Transfer);
			var lease = await persistence.TryAcquireWriteAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The first writer must acquire the lease.");
			await using (lease)
			{
				var data = await persistence.LoadForUpdateAsync(CancellationToken.None)
					?? throw new InvalidOperationException("An initialised database must load.");
				data.User.DismissSuggestion("dismissed");
				(await persistence.SaveAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
			}
		}

		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(old.Count + 1);
		(await ReadAsync(db => db.Categories.SingleAsync(c => c.Id == CategoryId.LoanReceived))).BuiltIn.Should().BeTrue();
	}

	/// <summary>Verifies a database stored before the built-in <c>borrowed-people</c> category existed shows it on a read, below
	/// Loan received, and stores it with the next decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_OldTreeWithoutBorrowedPeople_ReadShowsItAndDecisionAddsIt()
	{
		var old = CategoryDefaults.CreateDefaults("BE")
			.Where(c => c.Id != CategoryId.BorrowedPeople).ToList();
		await AddAsync([UserData.Empty(), .. old]);
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var persistence = scope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
			var read = await persistence.ReadAsync(CancellationToken.None);
			read.Categories.Should().Contain(c => c.Id == CategoryId.BorrowedPeople && c.ParentId == CategoryId.LoanReceived && c.Type == CategoryType.Transfer);
			var lease = await persistence.TryAcquireWriteAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The first writer must acquire the lease.");
			await using (lease)
			{
				var data = await persistence.LoadForUpdateAsync(CancellationToken.None)
					?? throw new InvalidOperationException("An initialised database must load.");
				data.User.DismissSuggestion("dismissed");
				(await persistence.SaveAsync(CancellationToken.None)).IsSuccess.Should().BeTrue();
			}
		}

		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(old.Count + 1);
		var stored = await ReadAsync(db => db.Categories.SingleAsync(c => c.Id == CategoryId.BorrowedPeople));
		stored.BuiltIn.Should().BeTrue();
		stored.ParentId.Should().Be(CategoryId.LoanReceived);
	}

	/// <summary>Verifies independent backend scopes cannot overwrite user decisions simultaneously.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_IndependentWriters_LeaseExclusive()
	{
		var firstScope = Services.CreateAsyncScope();
		await using (firstScope)
		{
			var secondScope = Services.CreateAsyncScope();
			await using (secondScope)
			{
				var first = firstScope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
				var second = secondScope.ServiceProvider.GetRequiredService<IWorkspacePersistence>();
				var lease = await first.TryAcquireWriteAsync(CancellationToken.None)
					?? throw new InvalidOperationException("The first writer must acquire the lease.");
				(await second.TryAcquireWriteAsync(CancellationToken.None)).Should().BeNull();
				await lease.DisposeAsync();
				var released = await second.TryAcquireWriteAsync(CancellationToken.None)
					?? throw new InvalidOperationException("The released lease must be available.");
				await released.DisposeAsync();
			}
		}
	}
	#endregion
}
