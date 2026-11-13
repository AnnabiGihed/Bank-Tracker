using System.Net;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.Shared.Features.Backend;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ApiProgram = KbcTracker.ApiService.Program;

namespace KbcTracker.E2E.Tests;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs the web and API hosts end to end over a disposable PostgreSQL database and checks what the web host
///              itself decides: that every tab address serves the Blazor shell, who may reach it and its circuit, the bank
///              callback, and the CSV export. What the components show and do is tested in the component tests; what the
///              backend stores in the API tests. Never touches personal data or the bank.
/// </summary>
/// <param name="fixture">The disposable database container shared by the class.</param>
public sealed class WebHostTests(BackendFixture fixture) : IClassFixture<BackendFixture>, IAsyncLifetime
{
	#region Constants
	/// <summary>The owner identity configured outside Development.</summary>
	private const string OwnerLogin = "owner@example.invalid";

	/// <summary>The negotiation request every Blazor circuit starts with.</summary>
	private const string NegotiatePath = "/_blazor/negotiate?negotiateVersion=1";
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

	#region Shell Tests
	/// <summary>Verifies the web host has no local data store or bank client after the cutover.</summary>
	[Fact]
	public void Services_ApiClientCutover_NoLocalPersistenceOrBankClient()
	{
		typeof(Program).Assembly.GetTypes().Should().NotContain(t => t.Namespace != null && t.Namespace.EndsWith(".Storage", StringComparison.Ordinal));
		Factory.Services.GetService<KbcTracker.Infrastructure.Features.BankConnection.EnableBankingClient>().Should().BeNull();
	}

	/// <summary>Verifies every tab address, the bank picker and the root answer with the Blazor shell: the Blazor script
	/// and the components' style bundle, no-store and no-referrer headers, and no page content rendered on the server
	/// (the circuit loads it).</summary>
	/// <param name="path">The address.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/")]
	[InlineData("/overview?m=2026-11")]
	[InlineData("/income?m=2026-11&show=refunds")]
	[InlineData("/fixed?m=2026-11")]
	[InlineData("/subscriptions?m=2026-11")]
	[InlineData("/transactions?m=2026-10&cat=other&q=shop")]
	[InlineData("/review?m=2026-11")]
	[InlineData("/categories?m=2026-11")]
	[InlineData("/settings?m=2026-11")]
	[InlineData("/banks")]
	public async Task Get_TabAddress_ServesBlazorShell(string path)
	{
		using var client = Factory.CreateClient();

		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
		var html = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
		response.Headers.CacheControl?.NoStore.Should().BeTrue();
		response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
		html.Should().MatchRegex(@"blazor\.web(\.[A-Za-z0-9]+)?\.js")
			.And.MatchRegex(@"KbcTracker\.Components(\.[A-Za-z0-9]+)?\.bundle\.scp\.css")
			.And.MatchRegex(@"Radzen\.Blazor(\.[A-Za-z0-9]+)?\.js")
			.And.NotContain("Shop One");
	}

	/// <summary>Verifies the circuit's negotiation request is served in Development from a loopback peer, the way a
	/// browser's own page sends it.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task PostNegotiate_DevelopmentSameOrigin_Served()
	{
		using var client = Factory.CreateClient();

		using var response = await SendWithIdentityAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), identity: null,
			request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
	#endregion

	#region Callback Tests
	/// <summary>Verifies a bank callback the backend accepts returns to Settings without a notice.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetCallback_BackendAccepts_RedirectsToSettings()
	{
		using var connected = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddHttpClient<BackendClient>().ConfigurePrimaryHttpMessageHandler(() => new ConnectedBackendHandler())));
		using var client = connected.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

		using var response = await client.GetAsync(new Uri("/callback?code=granted&state=known", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.OriginalString.Should().Be("/settings");
		response.Headers.Contains("Set-Cookie").Should().BeFalse();
	}

	/// <summary>Verifies a bank callback the backend rejects returns to Settings with the message in a short-lived,
	/// script-proof, HTTPS-only cookie instead of a raw problem document; an unknown state is rejected without
	/// contacting the bank.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetCallback_UnknownState_SettingsWithNoticeCookie()
	{
		using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

		using var response = await client.GetAsync(new Uri("/callback?code=unused&state=unknown-state", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.OriginalString.Should().Be("/settings");
		response.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie => cookie.StartsWith("kbc-notice=Bank%20connection%20failed", StringComparison.Ordinal)
			&& cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)
			&& cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Verifies the shell reads the notice cookie and clears it in the same response, so the message is shown
	/// once, and that a page opened without a notice sets no cookie.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_PageWithNoticeCookie_CookieClearedOnce()
	{
		using var client = Factory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/settings", UriKind.Relative));
		request.Headers.Add("Cookie", "kbc-notice=Bank%20connection%20failed");

		using var withNotice = await client.SendAsync(request);
		using var without = await client.GetAsync(new Uri("/settings", UriKind.Relative));

		withNotice.StatusCode.Should().Be(HttpStatusCode.OK);
		withNotice.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie => cookie.StartsWith("kbc-notice=;", StringComparison.Ordinal)
			&& cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
		without.Headers.Contains("Set-Cookie").Should().BeFalse();
	}
	#endregion

	#region Access Tests
	/// <summary>Verifies that outside Development the web app lets in only a loopback request carrying the owner's
	/// identity, on a tab address, on the circuit's negotiation and on the export: a missing or other identity and a
	/// home-network caller sending the owner's header are refused.</summary>
	/// <param name="path">The address, one the circuit or a download uses.</param>
	/// <param name="method">The request method.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/overview", "GET")]
	[InlineData("/export.csv", "GET")]
	[InlineData("/_blazor/negotiate?negotiateVersion=1", "POST")]
	public async Task Request_ProductionNonOwner_Forbidden(string path, string method)
	{
		using var production = Production(IPAddress.Loopback);
		using var client = production.CreateClient();
		using var network = Production(IPAddress.Parse("192.168.1.20"));
		using var networkClient = network.CreateClient();
		var address = new Uri(path, UriKind.Relative);
		var verb = new HttpMethod(method);
		void SameOrigin(HttpRequestMessage request) => request.Headers.Add("Sec-Fetch-Site", "same-origin");

		using var anonymous = await SendWithIdentityAsync(client, verb, address, identity: null, SameOrigin);
		using var stranger = await SendWithIdentityAsync(client, verb, address, "someone@example.invalid", SameOrigin);
		using var spoofed = await SendWithIdentityAsync(networkClient, verb, address, OwnerLogin, SameOrigin);
		using var owner = await SendWithIdentityAsync(client, verb, address, OwnerLogin, SameOrigin);

		anonymous.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		spoofed.StatusCode.Should().Be(HttpStatusCode.Forbidden, "only Tailscale Serve on this PC may supply the identity");
		owner.StatusCode.Should().Be(HttpStatusCode.OK, "the owner's own same-origin request, including the circuit's negotiation, must pass");
	}

	/// <summary>Verifies a write from another site is refused even from the owner, while the owner's own same-origin
	/// write (the circuit's negotiation, which a browser sends with <c>Sec-Fetch-Site: same-origin</c> and, under the
	/// no-referrer policy, <c>Origin: null</c>) and a navigation typed into the address bar pass. Another port on this PC
	/// and a forged origin without fetch metadata stay refused.</summary>
	/// <param name="site">The <c>Sec-Fetch-Site</c> header, or empty for none.</param>
	/// <param name="origin">The <c>Origin</c> header, or empty for none.</param>
	/// <param name="expected">The expected status.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("same-origin", "null", HttpStatusCode.OK)]
	[InlineData("none", "", HttpStatusCode.OK)]
	[InlineData("same-site", "https://localhost:17180", HttpStatusCode.Forbidden)]
	[InlineData("cross-site", "null", HttpStatusCode.Forbidden)]
	[InlineData("cross-site", "https://evil.example", HttpStatusCode.Forbidden)]
	[InlineData("", "null", HttpStatusCode.Forbidden)]
	[InlineData("", "https://evil.example", HttpStatusCode.Forbidden)]
	public async Task PostNegotiate_ProductionOwnerFetchMetadata_OnlyOwnPageWrites(string site, string origin, HttpStatusCode expected)
	{
		using var production = Production(IPAddress.Loopback);
		using var client = production.CreateClient();

		using var response = await SendWithIdentityAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), OwnerLogin, request =>
		{
			if (site.Length > 0)
			{
				request.Headers.Add("Sec-Fetch-Site", site);
			}

			if (origin.Length > 0)
			{
				request.Headers.Add("Origin", origin);
			}
		});

		response.StatusCode.Should().Be(expected);
	}

	/// <summary>Verifies a WebSocket upgrade to the Blazor circuit is judged like a write although it is a GET: a page on
	/// another site cannot open the socket that carries every change, while the app's own page is not refused.</summary>
	/// <param name="site">The <c>Sec-Fetch-Site</c> header, or empty for none.</param>
	/// <param name="origin">The <c>Origin</c> header, or empty for none.</param>
	/// <param name="refused">Whether the access check must refuse the upgrade.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("cross-site", "https://evil.example", true)]
	[InlineData("same-site", "https://localhost:17180", true)]
	[InlineData("", "https://evil.example", true)]
	[InlineData("same-origin", "https://localhost", false)]
	public async Task GetCircuitUpgrade_ProductionOwnerFetchMetadata_OnlyOwnPageConnects(string site, string origin, bool refused)
	{
		using var production = Production(IPAddress.Loopback);
		using var client = production.CreateClient();

		using var response = await SendWithIdentityAsync(client, HttpMethod.Get, new Uri("/_blazor", UriKind.Relative), OwnerLogin, request =>
		{
			request.Headers.Add("Connection", "Upgrade");
			request.Headers.Add("Upgrade", "websocket");
			if (site.Length > 0)
			{
				request.Headers.Add("Sec-Fetch-Site", site);
			}

			request.Headers.Add("Origin", origin);
		});

		if (refused)
		{
			response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		}
		else
		{
			response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
		}
	}
	#endregion

	#region Export Tests
	/// <summary>Verifies the CSV export is served as a download with its file name, and lists the history oldest first,
	/// with exact amounts and quoted text fields.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetExport_History_OldestFirstQuotedDownload()
	{
		using var client = Factory.CreateClient();

		using var response = await client.GetAsync(new Uri("/export.csv", UriKind.Relative));
		var lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
		response.Content.Headers.ContentDisposition?.FileName.Should().Be("kbc-transactions.csv");
		lines[0].Should().Be("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description;Part");
		lines[1].Should().StartWith("2026-10-03;-12.50;EUR;").And.EndWith(";\"SHOP ONE BE1000 Bruxelles Payment with card\";\"\"");
		lines[2].Should().StartWith("2026-11-03;2500;EUR;").And.EndWith(";\"Salary November\";\"\"");
	}

	/// <summary>Verifies the export follows a split payment: one line per part, with the part's amount and label, and the
	/// workspace the components rebuild their analysis from carries the same parts.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetExport_SplitPayment_OneLinePerPart()
	{
		var backend = Factory.Services.GetRequiredService<BackendClient>();
		var split = await backend.DecideAsync(new DecisionRequest("split", Id: "t1",
			Parts: [new SplitPartRequest(10m, "bills", "Water bottle"), new SplitPartRequest(2.50m, "cash", "Cash back")]), CancellationToken.None);
		using var client = Factory.CreateClient();

		var export = (await client.GetStringAsync(new Uri("/export.csv", UriKind.Relative))).Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var item = (await backend.GetWorkspaceAsync(CancellationToken.None)).ToAnalysis().Items.Single(i => i.T.Id.Equals(new TxnId("t1")));

		split.IsSuccess.Should().BeTrue();
		export.Where(l => l.StartsWith("2026-10-03;", StringComparison.Ordinal)).Should().HaveCount(2)
			.And.Contain(l => l.StartsWith("2026-10-03;-10", StringComparison.Ordinal) && l.EndsWith(";\"Water bottle\"", StringComparison.Ordinal))
			.And.Contain(l => l.StartsWith("2026-10-03;-2.5", StringComparison.Ordinal) && l.EndsWith(";\"Cash back\"", StringComparison.Ordinal));
		item.IsSplit.Should().BeTrue();
		item.Parts.Select(p => (p.Category.Id.Value, p.Amount, p.Label)).Should().Equal(("bills", -10m, "Water bottle"), ("cash", -2.50m, "Cash back"));
	}

	/// <summary>Verifies backend failure on the export returns a safe problem instead of reading a local history
	/// fallback, and no transport details.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetExport_BackendUnavailable_ServiceUnavailableWithoutTransportDetails()
	{
		using var unavailable = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddHttpClient<BackendClient>().ConfigurePrimaryHttpMessageHandler(() => new UnavailableBackendHandler())));
		using var client = unavailable.CreateClient();

		using var response = await client.GetAsync(new Uri("/export.csv", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
		response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
		(await response.Content.ReadAsStringAsync()).Should().Contain("Backend unavailable").And.NotContain("private transport details");
	}

	/// <summary>Verifies a backend refusal on the export is shown as what it is, not as an unavailable backend: 403 as a
	/// refused access with what to check, and 409 with the backend's own reason, since a conflict has several causes.</summary>
	/// <param name="status">The backend's answer to the workspace request.</param>
	/// <param name="expected">Text the answer must contain.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(HttpStatusCode.Forbidden, "Backend refused access")]
	[InlineData(HttpStatusCode.Conflict, "Bank access is not set up on the backend.")]
	public async Task GetExport_BackendRefuses_ProblemWithSameStatus(HttpStatusCode status, string expected)
	{
		using var refusing = WithRefusingBackend("api/v1/workspace", status, "Bank access is not set up on the backend.");
		using var client = refusing.CreateClient();

		using var response = await client.GetAsync(new Uri("/export.csv", UriKind.Relative));

		response.StatusCode.Should().Be(status);
		(await response.Content.ReadAsStringAsync()).Should().Contain(expected).And.NotContain("Backend unavailable");
	}
	#endregion

	#region Private Helpers
	/// <summary>Starts a web host outside Development with the owner configured, as Tailscale Serve on this PC or a
	/// caller on the home network would reach it.</summary>
	private WebApplicationFactory<Program> Production(IPAddress caller) => Factory.WithWebHostBuilder(builder => builder
		.UseEnvironment("Production")
		.UseSetting("Backend:OwnerLogin", OwnerLogin)
		.ConfigureTestServices(services => services.AddTransient<IStartupFilter>(_ => new RemoteAddressStartupFilter(caller))));

	/// <summary>Starts a web host whose backend answers one route with a problem and forwards the rest to the real API.</summary>
	private WebApplicationFactory<Program> WithRefusingBackend(string route, HttpStatusCode status, string detail) =>
		Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
			.ConfigurePrimaryHttpMessageHandler(() => new RefusingBackendHandler(route, status, detail) { InnerHandler = Api.Server.CreateHandler() })));

	/// <summary>Sends a request, carrying a Tailscale identity as Tailscale Serve would when one is given.</summary>
	private static async Task<HttpResponseMessage> SendWithIdentityAsync(HttpClient client, HttpMethod method, Uri path, string? identity,
		Action<HttpRequestMessage>? configure = null)
	{
		using var request = new HttpRequestMessage(method, path);
		if (identity is not null)
		{
			request.Headers.Add("Tailscale-User-Login", identity);
		}

		if (method == HttpMethod.Post)
		{
			request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "text/plain");
		}

		configure?.Invoke(request);
		return await client.SendAsync(request).ConfigureAwait(false);
	}

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
	/// Purpose     : Answers every request as a connected bank, so the web app's redirect is what is tested.
	/// </summary>
	private sealed class ConnectedBackendHandler : HttpMessageHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("""{"connected":true,"skippedTransactions":0,"syncFailed":false}""", System.Text.Encoding.UTF8, "application/json"),
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
	///              passes every other request to the real API.
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
	#endregion
}
