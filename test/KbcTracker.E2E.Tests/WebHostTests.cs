using System.Net;
using System.Net.Http.Json;
using KbcTracker.Domain.Features.BankConnection.Aggregates;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Categories.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.Shared.Features.Backend;
using Microsoft.Extensions.DependencyInjection;
using ApiProgram = KbcTracker.ApiService.Program;

namespace KbcTracker.E2E.Tests;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs the web and API hosts end to end over a disposable PostgreSQL database:
///              every tab renders, and actions reach central persistence through HTTP. Never touches personal data
///              or the bank.
/// </summary>
/// <param name="fixture">The disposable database container shared by the class.</param>
public sealed class WebHostTests(BackendFixture fixture) : IClassFixture<BackendFixture>, IAsyncLifetime
{
	#region Constants
	/// <summary>The owner identity configured outside Development.</summary>
	private const string OwnerLogin = "owner@example.invalid";
	#endregion

	#region Fields
	/// <summary>The API host that owns persistence and application operations.</summary>
	private WebApplicationFactory<ApiProgram>? _api;

	/// <summary>The UI host that communicates with the API over its HTTP test transport.</summary>
	private WebApplicationFactory<Program>? _web;
	#endregion

	#region Properties
	/// <summary>Gets the started UI host.</summary>
	private WebApplicationFactory<Program> Factory => _web ?? throw new InvalidOperationException("The web host is not started.");

	/// <summary>Gets the started API host.</summary>
	private WebApplicationFactory<ApiProgram> Api => _api ?? throw new InvalidOperationException("The API host is not started.");
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		_api = new WebApplicationFactory<ApiProgram>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Development")
			.UseSetting("ConnectionStrings:KbcTracker", fixture.CreateConnectionString())
			.UseSetting("AutoSyncHours", "0"));
		await ChangeAsync(async db =>
		{
			await db.Database.MigrateAsync().ConfigureAwait(false);
			db.Transactions.AddRange(
				Txn("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
				Txn("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"));
			db.Categories.AddRange(CategoryDefaults.Create(new Dictionary<string, string[]>(StringComparer.Ordinal)));
			db.UserData.Add(UserData.Empty());
		}).ConfigureAwait(false);
		_web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Development")
			.UseSetting("Backend:BaseUrl", "http://localhost/")
			.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
				.ConfigurePrimaryHttpMessageHandler(() => Api.Server.CreateHandler())));
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_web is not null)
		{
			await _web.DisposeAsync().ConfigureAwait(false);
		}

		if (_api is not null)
		{
			await _api.DisposeAsync().ConfigureAwait(false);
		}
	}
	#endregion

	#region Page Tests
	/// <summary>Verifies the web host has no local data store or bank client after the cutover.</summary>
	[Fact]
	public void Services_ApiClientCutover_NoLocalPersistenceOrBankClient()
	{
		typeof(Program).Assembly.GetTypes().Should().NotContain(t => t.Namespace != null && t.Namespace.EndsWith(".Storage", StringComparison.Ordinal));
		Factory.Services.GetService<KbcTracker.Infrastructure.Features.BankConnection.EnableBankingClient>().Should().BeNull();
	}

	/// <summary>Verifies backend failure returns a safe error instead of reading a local history fallback.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_BackendUnavailable_ServiceUnavailableWithoutTransportDetails()
	{
		using var unavailable = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddHttpClient<BackendClient>().ConfigurePrimaryHttpMessageHandler(() => new UnavailableBackendHandler())));
		using var client = unavailable.CreateClient();

		using var response = await client.GetAsync(new Uri("/overview", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
		(await response.Content.ReadAsStringAsync()).Should().Contain("Backend unavailable").And.NotContain("private transport details");
	}

	/// <summary>
	/// Verifies that a backend refusal while rendering a page is shown as what it is, not as an unavailable backend:
	/// 403 as a refused access and 409 as a backend that is not ready.
	/// </summary>
	/// <param name="status">The backend's answer to the workspace request.</param>
	/// <param name="expected">The heading the page must show.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(HttpStatusCode.Forbidden, "Backend refused access")]
	[InlineData(HttpStatusCode.Conflict, "Backend not ready")]
	public async Task Get_BackendRefuses_ReadablePageWithSameStatus(HttpStatusCode status, string expected)
	{
		using var refusing = WithRefusingBackend("api/v1/workspace", status, "private backend detail");
		using var client = refusing.CreateClient();

		using var response = await client.GetAsync(new Uri("/overview", UriKind.Relative));

		response.StatusCode.Should().Be(status);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
		(await response.Content.ReadAsStringAsync()).Should().Contain(expected).And.NotContain("Backend unavailable");
	}

	/// <summary>
	/// Verifies that every tab renders for a month with data.
	/// </summary>
	/// <param name="path">The tab's address.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("/overview?m=2026-11")]
	[InlineData("/fixed?m=2026-11")]
	[InlineData("/transactions?m=2026-10")]
	[InlineData("/review?m=2026-11")]
	[InlineData("/categories?m=2026-11")]
	[InlineData("/settings?m=2026-11")]
	public async Task Get_Tab_RendersHtml(string path)
	{
		using var client = Factory.CreateClient();

		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
		(await response.Content.ReadAsStringAsync()).Should().Contain("<html");
	}

	/// <summary>
	/// Verifies that the transactions tab lists a stored transaction.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Get_Transactions_ListsStoredTransaction()
	{
		using var client = Factory.CreateClient();

		var html = await client.GetStringAsync(new Uri("/transactions?m=2026-10", UriKind.Relative));

		html.Should().Contain("Shop One");
	}

	/// <summary>
	/// Verifies the sync notices on a page: nothing when no transaction was left out, the number left out with a
	/// link to sync the whole history again, and only the failure banner when the last sync failed.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Get_SessionWithSkippedTransactions_NoticeShownUnlessSyncFailed()
	{
		using var client = Factory.CreateClient();

		var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

		await SaveSessionAsync(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: null, skippedTransactions: 0));
		var withoutSkipped = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));
		await SaveSessionAsync(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: null, skippedTransactions: 3));
		var afterSkipped = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));
		await SaveSessionAsync(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: "Bank down", skippedTransactions: 3));
		var afterFailure = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));

		withoutSkipped.Should().NotContain("could not be read");
		afterSkipped.Should().Contain("3 transaction(s) from the bank could not be read").And.Contain("action='/sync?full=true&back=/settings'");
		afterFailure.Should().Contain("Last sync failed").And.NotContain("could not be read");
	}
	#endregion

	#region Api Tests
	/// <summary>
	/// Verifies that renaming a payee through <c>POST /api</c> is saved in the user's decisions.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_Rename_SavedInUserData()
	{
		using var client = Factory.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api", UriKind.Relative), new { action = "rename", key = "CARD:SHOP ONE", name = "My shop" });

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		(await LoadUserDataAsync()).PayeeNames["CARD:SHOP ONE"].Should().Be("My shop");
	}

	/// <summary>
	/// Verifies that requests missing a field the action needs, or with a blank name, are refused with 400 and a
	/// message instead of failing with 500.
	/// </summary>
	/// <param name="body">The request body.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("""{"action":"removeFixed"}""")]
	[InlineData("""{"action":"txnOnly","key":"CARD:SHOP ONE","id":"t1"}""")]
	[InlineData("""{"action":"rename","key":"CARD:SHOP ONE","name":" "}""")]
	[InlineData("""{"action":"saveCategory","name":"&&","type":"Expense","color":"#123456"}""")]
	[InlineData("""{"action":"unknown"}""")]
	public async Task PostApi_MissingOrInvalidField_BadRequest(string body)
	{
		using var client = Factory.CreateClient();
		using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

		using var response = await client.PostAsync(new Uri("/api", UriKind.Relative), content);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Verifies that actions on something that does not exist answer 404 with a message instead of failing.
	/// </summary>
	/// <param name="body">The request body.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("""{"action":"accept","key":"NO:SUCH"}""")]
	[InlineData("""{"action":"mark","key":"NO:SUCH"}""")]
	[InlineData("""{"action":"removeFixed","id":"missing"}""")]
	[InlineData("""{"action":"deleteCategory","id":"missing"}""")]
	public async Task PostApi_UnknownTarget_NotFoundWithMessage(string body)
	{
		using var client = Factory.CreateClient();
		using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

		using var response = await client.PostAsync(new Uri("/api", UriKind.Relative), content);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Verifies that dismissing a suggestion stores its signature, and that confirming payees through the single
	/// key or the list of keys stores their automatic category as confirmed.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_DismissAndConfirm_Saved()
	{
		using var client = Factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);

		var analysis = await AnalyseAsync();
		var keys = analysis.Items.Select(i => i.SeriesKey).ToList();

		using var dismiss = await client.PostAsJsonAsync(api, new { action = "dismiss", key = "SIGNATURE:X" });
		using var confirmOne = await client.PostAsJsonAsync(api, new { action = "confirm", key = keys[0] });
		using var confirmAll = await client.PostAsJsonAsync(api, new { action = "confirm", keys });

		dismiss.StatusCode.Should().Be(HttpStatusCode.OK);
		confirmOne.StatusCode.Should().Be(HttpStatusCode.OK);
		confirmAll.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = await LoadUserDataAsync();
		user.DismissedSuggestions.Should().Contain("SIGNATURE:X");
		user.Payees.Keys.Should().BeEquivalentTo(keys);
		user.Payees.Values.Should().OnlyContain(p => p.Confirmed);
	}

	/// <summary>
	/// Verifies the payee-category flow of the Transactions tab: setting a payee category returns the previous
	/// assignment, and "only this one" sends it back to restore the payee and set the transaction's own category.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_PayeeCategoryThenOnlyThisOne_PayeeRestoredTransactionSet()
	{
		using var client = Factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);
		using var first = await client.PostAsJsonAsync(api, new { action = "payeeCategory", key = "CARD:SHOP ONE", categoryId = "bills" });
		using var second = await client.PostAsJsonAsync(api, new { action = "payeeCategory", key = "CARD:SHOP ONE", categoryId = "cash" });
		var previous = await second.Content.ReadFromJsonAsync<PreviousAssignment>();

		using var onlyThis = await client.PostAsJsonAsync(api, new { action = "txnOnly", key = "CARD:SHOP ONE", id = "t1", categoryId = "cash", restore = previous });

		previous.Should().Be(new PreviousAssignment("bills", Confirmed: true));
		onlyThis.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = await LoadUserDataAsync();
		user.Payees["CARD:SHOP ONE"].CategoryId.Should().Be(new CategoryId("bills"));
		user.TxnCategories.Single().Value.Value.Should().Be("cash");
	}

	/// <summary>
	/// Verifies the fixed-payment flow: marking a payee as fixed adds it, and stopping it removes it and dismisses
	/// its suggestion so it does not come back.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_MarkThenRemoveFixed_RemovedAndDismissed()
	{
		using var client = Factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);

		var analysis = await AnalyseAsync();
		var seriesKey = analysis.Items.Single(i => i.T.Id.Equals(new TxnId("t1"))).SeriesKey;

		using var mark = await client.PostAsJsonAsync(api, new { action = "mark", key = seriesKey });
		var fixedPayment = (await LoadUserDataAsync()).Recurring.Single();
		using var remove = await client.PostAsJsonAsync(api, new { action = "removeFixed", id = fixedPayment.Id.Value });

		mark.StatusCode.Should().Be(HttpStatusCode.OK);
		remove.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = await LoadUserDataAsync();
		user.Recurring.Should().BeEmpty();
		user.DismissedSuggestions.Should().Contain(fixedPayment.Signature);
	}

	/// <summary>
	/// Verifies that changing a fixed payment that does not exist answers 404 instead of failing.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_UpdateUnknownFixedPayment_NotFound()
	{
		using var client = Factory.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api", UriKind.Relative), new { action = "updateFixed", id = "missing", name = "Rent" });

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>
	/// Verifies that the built-in <c>other</c> category cannot be deleted, and that a new category can be created
	/// and deleted.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_Categories_OtherProtectedAndNewOneDeletable()
	{
		using var client = Factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);

		using var deleteOther = await client.PostAsJsonAsync(api, new { action = "deleteCategory", id = "other" });
		using var save = await client.PostAsJsonAsync(api, new { action = "saveCategory", name = "Pets", type = "Expense", color = "#123456", keywords = "VET, ZOO" });
		var created = await save.Content.ReadFromJsonAsync<CreatedCategory>();
		created.Should().NotBeNull();
		using var deleteNew = await client.PostAsJsonAsync(api, new { action = "deleteCategory", id = created.Id });

		deleteOther.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		save.StatusCode.Should().Be(HttpStatusCode.OK);
		created.Id.Should().Be("pets");
		deleteNew.StatusCode.Should().Be(HttpStatusCode.OK);
	}
	#endregion

	#region Sync Tests
	/// <summary>
	/// Verifies that after a sync the web app returns only to a page of its own: a local return path is kept, and
	/// anything a browser could read as another site falls back to Settings.
	/// </summary>
	/// <param name="back">The requested return path.</param>
	/// <param name="expected">The expected redirect.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("/fixed?m=2026-11", "/fixed?m=2026-11")]
	[InlineData("//evil.example/", "/settings")]
	[InlineData("/\\evil.example/", "/settings")]
	[InlineData("/\t/evil.example/", "/settings")]
	[InlineData("https://evil.example/", "/settings")]
	[InlineData("", "/settings")]
	public async Task PostSync_ReturnPath_OnlyLocalKept(string back, string expected)
	{
		using var synced = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddHttpClient<BackendClient>().ConfigurePrimaryHttpMessageHandler(() => new SyncedBackendHandler())));
		using var client = synced.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		using var response = await client.PostAsync(new Uri("/sync?back=" + Uri.EscapeDataString(back), UriKind.Relative), content: null);

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.OriginalString.Should().Be(expected);
	}

	/// <summary>
	/// Verifies that a refused sync returns the browser to the page it came from with the backend's message, shown
	/// once, instead of a raw problem document.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostSync_BackendRefuses_ReturnsToPageWithOneTimeNotice()
	{
		using var refusing = WithRefusingBackend("api/v1/sync", HttpStatusCode.Conflict, "Another sync is running.");
		using var client = refusing.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

		using var response = await client.PostAsync(new Uri("/sync?back=" + Uri.EscapeDataString("/fixed?m=2026-11"), UriKind.Relative), content: null);
		var page = await response.Content.ReadAsStringAsync();
		var again = await client.GetStringAsync(new Uri("/fixed?m=2026-11", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.RequestMessage!.RequestUri!.PathAndQuery.Should().Be("/fixed?m=2026-11");
		page.Should().Contain("Sync failed: Another sync is running.");
		again.Should().NotContain("Sync failed");
	}

	/// <summary>
	/// Verifies that a bank callback the backend rejects returns to Settings with a notice instead of a raw problem
	/// document; an unknown state is rejected without contacting the bank.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task GetCallback_UnknownState_SettingsWithNotice()
	{
		using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

		using var response = await client.GetAsync(new Uri("/callback?code=unused&state=unknown-state", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/settings");
		(await response.Content.ReadAsStringAsync()).Should().Contain("Bank connection failed:");
	}

	/// <summary>
	/// Verifies that a refused bank connection returns to the bank picker and leaves the message for it.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostConnect_BackendRefuses_BankPickerWithNotice()
	{
		using var refusing = WithRefusingBackend("api/v1/consent", HttpStatusCode.Conflict, "The backend has no data yet.");
		using var client = refusing.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

		using var response = await client.PostAsync(new Uri("/connect?name=KBC&country=BE", UriKind.Relative), content: null);

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.OriginalString.Should().Be("/banks");
		response.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie => cookie.StartsWith("kbc-notice=", StringComparison.Ordinal)
			&& cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Verifies that the bank picker shows a refused connection's message once, encoded, so markup in the backend's
	/// answer can never run in the page.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostConnect_BackendRefusesWithMarkup_BankPickerShowsEncodedNoticeOnce()
	{
		using var refusing = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
			.ConfigurePrimaryHttpMessageHandler(() => new RefusingBackendHandler("api/v1/consent", HttpStatusCode.Conflict, "<script>alert(1)</script>")
			{
				InnerHandler = new OneBankBackendHandler { InnerHandler = Api.Server.CreateHandler() },
			})));
		using var client = refusing.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

		using var response = await client.PostAsync(new Uri("/connect?name=KBC&country=BE", UriKind.Relative), content: null);
		var page = await response.Content.ReadAsStringAsync();
		var again = await client.GetStringAsync(new Uri("/banks", UriKind.Relative));

		response.RequestMessage!.RequestUri!.AbsolutePath.Should().Be("/banks");
		page.Should().Contain("Could not connect to the bank: &lt;script&gt;alert(1)&lt;/script&gt;").And.NotContain("<script>alert(1)");
		again.Should().NotContain("Could not connect to the bank");
	}
	#endregion

	#region Access And Export Tests
	/// <summary>
	/// Verifies that outside Development the web app lets in only a loopback request carrying the owner's identity:
	/// a home-network caller sending the owner's header is refused, and so is a write from another site even from
	/// the owner.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Get_ProductionNonOwnerOrCrossSite_Forbidden()
	{
		using var production = Factory.WithWebHostBuilder(builder => builder
			.UseEnvironment("Production")
			.UseSetting("Backend:OwnerLogin", OwnerLogin)
			.ConfigureTestServices(services => services.AddTransient<IStartupFilter>(_ => new RemoteAddressStartupFilter(IPAddress.Loopback))));
		using var client = production.CreateClient();
		using var network = production.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddTransient<IStartupFilter>(_ => new RemoteAddressStartupFilter(IPAddress.Parse("192.168.1.20")))));
		using var networkClient = network.CreateClient();
		var overview = new Uri("/overview", UriKind.Relative);

		using var anonymous = await client.GetAsync(overview);
		using var stranger = await SendWithIdentityAsync(client, HttpMethod.Get, overview, "someone@example.invalid");
		using var owner = await SendWithIdentityAsync(client, HttpMethod.Get, overview, OwnerLogin);
		using var spoofed = await SendWithIdentityAsync(networkClient, HttpMethod.Get, overview, OwnerLogin);
		using var crossOrigin = await SendWithIdentityAsync(client, HttpMethod.Post, new Uri("/api", UriKind.Relative), OwnerLogin,
			request => request.Headers.Add("Origin", "https://evil.example"));
		using var crossSite = await SendWithIdentityAsync(client, HttpMethod.Post, new Uri("/api", UriKind.Relative), OwnerLogin,
			request => request.Headers.Add("Sec-Fetch-Site", "cross-site"));

		anonymous.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		owner.StatusCode.Should().Be(HttpStatusCode.OK);
		spoofed.StatusCode.Should().Be(HttpStatusCode.Forbidden, "only Tailscale Serve on this PC may supply the identity");
		crossOrigin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		crossSite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>
	/// Verifies the CSV export lists the history oldest first, with exact amounts and quoted text fields.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task GetExport_History_OldestFirstQuoted()
	{
		using var client = Factory.CreateClient();

		var lines = (await client.GetStringAsync(new Uri("/export.csv", UriKind.Relative))).Split('\n', StringSplitOptions.RemoveEmptyEntries);

		lines[0].Should().Be("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description");
		lines[1].Should().StartWith("2026-10-03;-12.50;EUR;").And.EndWith(";\"SHOP ONE BE1000 Bruxelles Payment with card\"");
		lines[2].Should().StartWith("2026-11-03;2500;EUR;").And.EndWith(";\"Salary November\"");
	}
	#endregion

	#region Private Helpers
	/// <summary>Starts a web host whose backend answers one route with a problem and forwards the rest to the real API.</summary>
	private WebApplicationFactory<Program> WithRefusingBackend(string route, HttpStatusCode status, string detail) =>
		Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
			.ConfigurePrimaryHttpMessageHandler(() => new RefusingBackendHandler(route, status, detail) { InnerHandler = Api.Server.CreateHandler() })));

	/// <summary>Sends a request carrying a Tailscale identity, as Tailscale Serve would.</summary>
	private static async Task<HttpResponseMessage> SendWithIdentityAsync(HttpClient client, HttpMethod method, Uri path, string identity,
		Action<HttpRequestMessage>? configure = null)
	{
		using var request = new HttpRequestMessage(method, path);
		request.Headers.Add("Tailscale-User-Login", identity);
		if (method == HttpMethod.Post)
		{
			request.Content = new StringContent("""{"action":"dismiss","key":"SIGNATURE:CROSS"}""", System.Text.Encoding.UTF8, "application/json");
		}

		configure?.Invoke(request);
		return await client.SendAsync(request).ConfigureAwait(false);
	}

	/// <summary>Reads the centrally stored user decisions through a fresh database scope.</summary>
	private Task<UserData> LoadUserDataAsync() => ReadAsync(db => db.UserData.AsNoTracking().SingleAsync());

	/// <summary>Builds the same analysis as the backend for selecting test action targets.</summary>
	private async Task<Analysis> AnalyseAsync()
	{
		var transactions = await ReadAsync(db => db.Transactions.AsNoTracking().ToListAsync()).ConfigureAwait(false);
		var categories = await ReadAsync(db => db.Categories.AsNoTracking().ToListAsync()).ConfigureAwait(false);
		return new Analysis(transactions, categories, await LoadUserDataAsync().ConfigureAwait(false));
	}

	/// <summary>Replaces a test consent while retaining EF concurrency metadata.</summary>
	private Task SaveSessionAsync(SessionInfo session) => ChangeAsync(async db =>
	{
		var existing = await db.BankSessions.SingleOrDefaultAsync().ConfigureAwait(false);
		if (existing is null)
		{
			db.BankSessions.Add(session);
		}
		else
		{
			db.Entry(existing).CurrentValues.SetValues(session);
		}
	});

	/// <summary>Persists test setup in its own scope rather than sharing request change trackers.</summary>
	private async Task ChangeAsync(Func<KbcTrackerDbContext, Task> change)
	{
		var scope = Api.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			await change(db).ConfigureAwait(false);
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Reads durable results independently of the requests that wrote them.</summary>
	private async Task<T> ReadAsync<T>(Func<KbcTrackerDbContext, Task<T>> read)
	{
		var scope = Api.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			return await read(scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>()).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// A transaction of the test account without bank JSON, as older histories hold.
	/// </summary>
	private static Txn Txn(string id, DateOnly date, decimal amount, string counterparty, string description) =>
		Domain.Features.Transactions.Aggregates.Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate(counterparty, null), description, BankRecord.None);
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Sets the caller's address, which TestServer does not otherwise populate: loopback models Tailscale
	///              Serve on this PC, another address a caller on the home network.
	/// </summary>
	/// <param name="address">The caller's address.</param>
	private sealed class RemoteAddressStartupFilter(IPAddress address) : IStartupFilter
	{
		#region IStartupFilter Implementation
		/// <inheritdoc />
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
		{
			app.Use(async (context, continuation) =>
			{
				context.Connection.RemoteIpAddress = address;
				await continuation(context).ConfigureAwait(false);
			});
			next(app);
		};
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers every request as a successful sync, so the web app's redirect is what is tested.
	/// </summary>
	private sealed class SyncedBackendHandler : HttpMessageHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("""{"added":0}""", System.Text.Encoding.UTF8, "application/json"),
			});
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Reproduces an unreachable API without relying on a real network or port race.
	/// </summary>
	private sealed class UnavailableBackendHandler : HttpMessageHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromException<HttpResponseMessage>(new HttpRequestException("private transport details"));
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers one backend route with a problem document, as the API does when it refuses a request, and
	///              passes every other request to the real API so the pages around it still render.
	/// </summary>
	/// <param name="route">The path of the refused route, relative to the backend's base address.</param>
	/// <param name="status">The refusal's status.</param>
	/// <param name="detail">The problem's detail.</param>
	private sealed class RefusingBackendHandler(string route, HttpStatusCode status, string detail) : DelegatingHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (!string.Equals(request.RequestUri?.AbsolutePath.TrimStart('/'), route, StringComparison.Ordinal))
			{
				return base.SendAsync(request, cancellationToken);
			}

			var problem = System.Text.Json.JsonSerializer.Serialize(new { title = "Refused.", status = (int)status, detail });
			return Task.FromResult(new HttpResponseMessage(status)
			{
				Content = new StringContent(problem, System.Text.Encoding.UTF8, "application/problem+json"),
			});
		}
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers the bank list with one bank, which the API cannot do in tests without an Enable Banking
	///              application, and passes every other request on.
	/// </summary>
	private sealed class OneBankBackendHandler : DelegatingHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (!string.Equals(request.RequestUri?.AbsolutePath, "/api/v1/banks", StringComparison.Ordinal))
			{
				return base.SendAsync(request, cancellationToken);
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("""[{"name":"KBC","country":"BE"}]""", System.Text.Encoding.UTF8, "application/json"),
			});
		}
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The answer of <c>saveCategory</c>.
	/// </summary>
	/// <param name="Id">The category id.</param>
	private sealed record CreatedCategory(string Id);

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The answer of <c>payeeCategory</c>: the payee's assignment before the change.
	/// </summary>
	/// <param name="CategoryId">The previous category id.</param>
	/// <param name="Confirmed">Whether it was confirmed.</param>
	private sealed record PreviousAssignment(string? CategoryId, bool Confirmed);
	#endregion
}
