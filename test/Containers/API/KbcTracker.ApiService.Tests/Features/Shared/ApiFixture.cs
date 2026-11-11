using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Domain.Features.BankConnection.Aggregates;
using KbcTracker.Domain.Features.Categories.Aggregates;
using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace KbcTracker.ApiService.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Hosts the real API against a disposable migrated PostgreSQL database and a fake bank.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
	#region Constants
	/// <summary>The fake owner identity used only by local test requests.</summary>
	public const string OwnerLogin = "owner@example.invalid";
	/// <summary>A marker proving API responses do not disclose provider session credentials.</summary>
	public const string PrivateSession = "private-session-do-not-expose";
	/// <summary>A marker proving raw bank JSON is not serialized to clients.</summary>
	public const string RawMarker = "secret-bank-json-marker";
	#endregion

	#region Fields
	private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
	private WebApplicationFactory<Program>? _factory;
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		await _database.StartAsync().ConfigureAwait(false);
		_factory = CreateFactory("Development");
		var scope = _factory.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			await db.Database.MigrateAsync().ConfigureAwait(false);
			var now = DateTimeOffset.UtcNow;
			db.Transactions.Add(Transaction("historical"));
			db.UserData.Add(UserData.Empty());
			db.Categories.Add(Category.Rehydrate(new CategoryId("other"), "Other", CategoryType.Expense, "#123456", [], builtIn: true));
			db.BankSessions.Add(SessionInfo.Rehydrate(new BankSessionId(PrivateSession), ["current"], now, now.AddDays(180), null,
				"Provider error containing private file paths", 0));
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_factory is not null)
		{
			await _factory.DisposeAsync().ConfigureAwait(false);
		}

		await _database.DisposeAsync().ConfigureAwait(false);
	}
	#endregion

	#region Public Methods
	/// <summary>Creates a caller-owned HTTP client for the development host.</summary>
	/// <returns>The client.</returns>
	public HttpClient CreateClient() => (_factory ?? throw new InvalidOperationException("The API is not started.")).CreateClient();

	/// <summary>Creates a separate environment over the same isolated test database.</summary>
	/// <param name="environment">The ASP.NET Core environment name.</param>
	/// <param name="consentSource">An optional provider double for consent requests.</param>
	/// <param name="databaseName">An optional isolated database within the disposable container.</param>
	/// <param name="transactionSource">An optional bank double for transaction downloads, to inspect its calls.</param>
	/// <returns>A caller-owned factory.</returns>
	public WebApplicationFactory<Program> CreateFactory(string environment, IBankConsentSource? consentSource = null, string? databaseName = null,
		IBankTransactionSource? transactionSource = null)
	{
		var connection = new Npgsql.NpgsqlConnectionStringBuilder(_database.GetConnectionString());
		if (databaseName is not null)
		{
			connection.Database = databaseName;
		}

		var bank = transactionSource ?? DownloadingSource();
		return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseEnvironment(environment)
			.UseSetting("AutoSyncHours", "0")
			.UseSetting("ConnectionStrings:KbcTracker", connection.ConnectionString)
			.UseSetting("BackendAccess:OwnerLogin", OwnerLogin)
			.ConfigureTestServices(services =>
			{
				services.RemoveAll<IBankTransactionSource>();
				services.AddSingleton(bank);
				if (consentSource is not null)
				{
					services.RemoveAll<IBankConsentSource>();
					services.AddSingleton(consentSource);
				}

				services.AddTransient<IStartupFilter, LoopbackTestStartupFilter>();
			}));
	}
	#endregion

	#region Public Methods
	/// <summary>Creates a bank double that returns one downloaded transaction for any account.</summary>
	/// <returns>The bank double.</returns>
	public static IBankTransactionSource DownloadingSource()
	{
		var bank = Substitute.For<IBankTransactionSource>();
		bank.GetTransactionsAsync(default!, default, default).ReturnsForAnyArgs(new BankDownload([Transaction("downloaded")], 0));
		return bank;
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates a bank transaction with source fields that must stay on the backend.</summary>
	private static Txn Transaction(string id) => Txn.Rehydrate(new TxnId(id), "current", new DateOnly(2026, 11, 2),
		Money.Rehydrate(-12.50m, "EUR"), Counterparty.Rehydrate("API Shop", "BE00111122223333"),
		"Payment", new BankRecord("PMNT", "{\"marker\":\"" + RawMarker + "\"}"));
	#endregion
}
