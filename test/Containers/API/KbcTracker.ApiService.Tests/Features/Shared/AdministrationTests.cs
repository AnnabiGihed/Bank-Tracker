using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KbcTracker.ApiService.Features.Shared;
using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Infrastructure.Features.Legacy.Documents;
using KbcTracker.Domain.Features.Categories.Aggregates;
using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exercises the administrative import and initialisation entry points against isolated migrated
///              PostgreSQL databases.
/// </summary>
/// <param name="fixture">The disposable PostgreSQL host.</param>
public sealed class AdministrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Command Tests
	/// <summary>Verifies explicit import preserves history and archive bytes, then refuses a populated target.</summary>
	/// <returns>A task that completes after verification.</returns>
	[Fact]
	public async Task ImportJson_EmptyThenPopulated_ImportsOnceAndPreservesArchive()
	{
		using var factory = fixture.CreateFactory("Development", databaseName: "import_" + Guid.NewGuid().ToString("N"));
		var archive = Path.Combine(Path.GetTempPath(), "kbc-import-command-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(archive);
		try
		{
			var transaction = Txn.Rehydrate(new TxnId("old-history"), "account", new DateOnly(2020, 1, 2),
				Money.Rehydrate(-12.3456m, "EUR"), Counterparty.Rehydrate("Archive shop", null), "Old payment", new BankRecord("PMNT", "{ \"original\": true }"));
			await File.WriteAllTextAsync(Path.Combine(archive, "transactions.json"), JsonSerializer.Serialize(new[] { TxnDocument.From(transaction) }));
			await File.WriteAllTextAsync(Path.Combine(archive, "categories.json"), JsonSerializer.Serialize(new[]
			{
				CategoryDocument.From(Category.Rehydrate(CategoryId.Other, "Other", CategoryType.Expense, "#123456", [], true)),
			}));
			await File.WriteAllTextAsync(Path.Combine(archive, "userdata.json"), JsonSerializer.Serialize(UserDataDocument.From(UserData.Empty())));
			var original = await File.ReadAllBytesAsync(Path.Combine(archive, "transactions.json"));
			(await BackendAdministration.ExecuteAsync(["--migrate"], factory.Services, CancellationToken.None)).Should().Be(0);

			(await BackendAdministration.ExecuteAsync(["--import-json", archive], factory.Services, CancellationToken.None)).Should().Be(0);
			(await BackendAdministration.ExecuteAsync(["--import-json", archive], factory.Services, CancellationToken.None)).Should().Be(1);

			var scope = factory.Services.CreateAsyncScope();
			await using (scope)
			{
				var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
				var stored = await db.Transactions.SingleAsync();
				stored.Amount.Should().Be(-12.3456m);
				stored.Raw.Should().Be(transaction.Raw);
				stored.Date.Should().Be(transaction.Date);
				(await db.UserData.CountAsync()).Should().Be(1);
			}

			(await File.ReadAllBytesAsync(Path.Combine(archive, "transactions.json"))).Should().Equal(original);
		}
		finally
		{
			Directory.Delete(archive, recursive: true);
		}
	}

	/// <summary>Verifies a database that was neither imported nor initialised refuses writes, so the one-time import
	/// stays possible, and that <c>--initialize</c> seeds it once, unlocks them, and on a second run (every AppHost start)
	/// succeeds without seeding again.</summary>
	/// <returns>A task that completes after verification.</returns>
	[Fact]
	public async Task Initialize_UninitialisedThenInitialised_RefusesWritesUntilSeededOnce()
	{
		var bank = Substitute.For<IBankConsentSource>();
		string? state = null;
		bank.BeginAsync(Arg.Any<BankDefinition>(), Arg.Do<string>(value => state = value), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new Uri("https://bank.example.invalid/consent")));
		bank.CompleteAsync("valid-code", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new BankConsentSession("early-session", ["account"])));
		using var factory = fixture.CreateFactory("Development", bank, databaseName: "initialize_" + Guid.NewGuid().ToString("N"));
		using var client = factory.CreateClient();
		(await BackendAdministration.ExecuteAsync(["--migrate"], factory.Services, CancellationToken.None)).Should().Be(0);

		using (var early = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest("dismiss", Key: "series")))
		{
			early.StatusCode.Should().Be(HttpStatusCode.Conflict);
		}

		using (var begin = await client.PostAsJsonAsync(new Uri("/api/v1/consent", UriKind.Relative), new BeginConsentRequest("KBC", "BE")))
		{
			begin.StatusCode.Should().Be(HttpStatusCode.OK);
		}

		using (var callback = await client.GetAsync(new Uri($"/api/v1/consent/callback?code=valid-code&state={state}", UriKind.Relative)))
		{
			callback.StatusCode.Should().Be(HttpStatusCode.Conflict);
		}

		(await CountsAsync(factory)).Should().Be((0, 0, 0));
		(await BackendAdministration.ExecuteAsync(["--initialize"], factory.Services, CancellationToken.None)).Should().Be(0);
		var seeded = await CountsAsync(factory);

		using (var accepted = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest("dismiss", Key: "series")))
		{
			accepted.StatusCode.Should().Be(HttpStatusCode.OK);
		}

		using (var deleted = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest("deleteCategory", Id: "car")))
		{
			deleted.StatusCode.Should().Be(HttpStatusCode.OK);
		}

		(await BackendAdministration.ExecuteAsync(["--initialize"], factory.Services, CancellationToken.None)).Should().Be(0);
		(await CountsAsync(factory)).Should().Be((seeded.Categories - 1, seeded.Users, seeded.Sessions),
			"a database that holds data is left alone: a category the owner deleted is not seeded back");

		var (categories, users, sessions) = await CountsAsync(factory);
		categories.Should().BeGreaterThan(1, "the default categories are seeded");
		users.Should().Be(1);
		sessions.Should().Be(0, "the refused consent was not saved");
	}

	/// <summary>Verifies malformed archive input exits unsuccessfully and leaves the database empty.</summary>
	/// <returns>A task that completes after verification.</returns>
	[Fact]
	public async Task ImportJson_MalformedArchive_ReturnsFailureWithoutWriting()
	{
		using var factory = fixture.CreateFactory("Development", databaseName: "invalid_import_" + Guid.NewGuid().ToString("N"));
		var archive = Path.Combine(Path.GetTempPath(), "kbc-invalid-import-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(archive);
		try
		{
			await File.WriteAllTextAsync(Path.Combine(archive, "transactions.json"), "{ invalid");
			(await BackendAdministration.ExecuteAsync(["--migrate"], factory.Services, CancellationToken.None)).Should().Be(0);
			(await BackendAdministration.ExecuteAsync(["--import-json", archive], factory.Services, CancellationToken.None)).Should().Be(1);
			var scope = factory.Services.CreateAsyncScope();
			await using (scope)
			{
				(await scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>().Transactions.CountAsync()).Should().Be(0);
			}
		}
		finally
		{
			Directory.Delete(archive, recursive: true);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Counts the stored categories, user decisions and bank connections.</summary>
	private static async Task<(int Categories, int Users, int Sessions)> CountsAsync(WebApplicationFactory<Program> factory)
	{
		var scope = factory.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			return (await db.Categories.CountAsync().ConfigureAwait(false), await db.UserData.CountAsync().ConfigureAwait(false),
				await db.BankSessions.CountAsync().ConfigureAwait(false));
		}
	}
	#endregion
}
