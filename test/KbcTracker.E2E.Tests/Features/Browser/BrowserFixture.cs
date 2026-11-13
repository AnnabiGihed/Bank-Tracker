using System.Net;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.Shared.Features.Backend;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using ApiProgram = KbcTracker.ApiService.Program;

namespace KbcTracker.E2E.Tests.Features.Browser;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Starts what a real browser needs, once per test class: a disposable PostgreSQL database with known
///              transactions, the API host over it, the web host on a real loopback Kestrel port (a browser cannot
///              talk to TestServer), and one headless Chromium. Chromium is downloaded by Playwright on first use, so
///              the first run needs network access and a few hundred MB of disk; later runs reuse the cached copy.
/// </summary>
public sealed class BrowserFixture : IAsyncLifetime
{
	#region Constants
	/// <summary>The id of the payment the browser test recategorises.</summary>
	public const string PaymentId = "t1";
	#endregion

	#region Fields
	/// <summary>The disposable PostgreSQL container shared by the two hosts.</summary>
	private readonly BackendFixture _database = new();

	/// <summary>The API host that owns persistence.</summary>
	private WebApplicationFactory<ApiProgram>? _api;

	/// <summary>The web host listening on a real port.</summary>
	private WebApplicationFactory<Program>? _web;

	/// <summary>The Playwright driver.</summary>
	private IPlaywright? _playwright;

	/// <summary>The headless Chromium.</summary>
	private IBrowser? _browser;
	#endregion

	#region Properties
	/// <summary>Gets the address of the web host, for example <c>http://127.0.0.1:41234/</c>.</summary>
	public Uri WebAddress { get; private set; } = new("http://127.0.0.1/");

	/// <summary>Gets the folder that receives a trace and a screenshot when a browser test fails.</summary>
	public static string ArtifactsFolder { get; } = Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

	/// <summary>Gets the started browser.</summary>
	private IBrowser Browser => _browser ?? throw new InvalidOperationException("The browser is not started.");
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		try
		{
			await StartAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
			// xUnit does not dispose a fixture whose initialisation failed, so release what was started before the failure surfaces.
			await DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_browser is not null)
		{
			await _browser.DisposeAsync().ConfigureAwait(false);
		}

		_playwright?.Dispose();
		if (_web is not null)
		{
			await _web.DisposeAsync().ConfigureAwait(false);
		}

		if (_api is not null)
		{
			await _api.DisposeAsync().ConfigureAwait(false);
		}

		await _database.DisposeAsync().ConfigureAwait(false);
	}
	#endregion

	#region Public Methods
	/// <summary>Opens a fresh browser context with tracing on, so a failing test can keep what happened.</summary>
	/// <returns>The context, which the caller disposes.</returns>
	public async Task<IBrowserContext> NewContextAsync()
	{
		var context = await Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = WebAddress.ToString() }).ConfigureAwait(false);
		await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = false }).ConfigureAwait(false);
		return context;
	}

	/// <summary>Reads the owner's stored decisions straight from the database, the way the API persisted them.</summary>
	/// <returns>The payee assignments and the per-payment categories, by payment id.</returns>
	public async Task<StoredDecisions> ReadDecisionsAsync()
	{
		var scope = _api!.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			var data = await db.UserData.AsNoTracking().SingleAsync().ConfigureAwait(false);
			return new StoredDecisions(
				[.. data.Payees.Keys],
				data.TxnCategories.ToDictionary(pair => pair.Key.Value, pair => pair.Value.Value, StringComparer.Ordinal));
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Starts the database, both hosts and the browser.</summary>
	/// <returns>A task that completes when everything is running.</returns>
	private async Task StartAsync()
	{
		await _database.InitializeAsync().ConfigureAwait(false);
		var api = new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Development")
			.UseSetting("ConnectionStrings:KbcTracker", _database.CreateConnectionString())
			.UseSetting("AutoSyncHours", "0"));
		_api = api;
		await SeedAsync().ConfigureAwait(false);
		var configured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Development")
			.UseSetting("Backend:BaseUrl", "http://localhost/")
			.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
				.ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler())));
		configured.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
		_web = configured;
		configured.StartServer();
		var address = configured.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
		WebAddress = new Uri(address ?? throw new InvalidOperationException("The web host reported no address."));
		IPAddress.TryParse(WebAddress.Host, out var bound).Should().BeTrue("the web host must report an IP address");
		IPAddress.IsLoopback(bound!).Should().BeTrue("the web host must listen on loopback only");

		Microsoft.Playwright.Program.Main(["install", "chromium"]).Should().Be(0, "Chromium must be installed for the browser test");
		_playwright = await Playwright.CreateAsync().ConfigureAwait(false);
		_browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
	}

	/// <summary>Migrates the database and stores two payments, one expense in October and the salary in November 2026.</summary>
	/// <returns>A task that completes when the data is stored.</returns>
	private async Task SeedAsync()
	{
		var scope = _api!.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			await db.Database.MigrateAsync().ConfigureAwait(false);
			db.Transactions.AddRange(
				Txn(PaymentId, new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
				Txn("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"));
			db.Categories.AddRange(CategoryDefaults.CreateDefaults("BE"));
			db.UserData.Add(UserData.Empty());
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Builds a payment of the test account without bank JSON.</summary>
	private static Txn Txn(string id, DateOnly date, decimal amount, string counterparty, string description) =>
		Domain.Features.Transactions.Aggregates.Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate(counterparty, null), description, BankRecord.None);
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The decisions the browser test checks.
	/// </summary>
	/// <param name="PayeeSeriesKeys">The payees that have a category assignment.</param>
	/// <param name="PaymentCategories">The category chosen for single payments, by payment id.</param>
	public sealed record StoredDecisions(IReadOnlyList<string> PayeeSeriesKeys, IReadOnlyDictionary<string, string> PaymentCategories);
	#endregion
}
