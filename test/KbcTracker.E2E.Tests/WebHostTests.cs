using System.Net;
using System.Security.Claims;
using KbcTracker.ApiService.Features.Authentication;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.E2E.Tests.Features.Authentication;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.Shared.Features.Backend;
using KbcTracker.Web.Features.Authentication;
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
///              itself decides: that every tab address serves the Blazor shell, that only the signed-in owner reaches it, its
///              circuit, the bank callback and the CSV export, the sign-in page and the sign-out, and the refusal of cross-site
///              writes. The owner signs in through the test-only route of <see cref="TestSignInStartupFilter"/>. What the components show and do is tested in the component tests; what the
///              backend stores in the API tests. Never touches personal data or the bank.
/// </summary>
/// <param name="fixture">The disposable database container shared by the class.</param>
public sealed class WebHostTests(BackendFixture fixture) : IClassFixture<BackendFixture>, IAsyncLifetime
{
	#region Constants
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
			.UseSetting("AutoSyncHours", "0")
			.UseTestSignIn());
		await ChangeAsync(async db =>
		{
			await db.Database.MigrateAsync().ConfigureAwait(false);
			db.Transactions.AddRange(
				Txn("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
				Txn("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"));
			db.Categories.AddRange(CategoryDefaults.Create(new Dictionary<string, string[]>(StringComparer.Ordinal)));
			db.UserData.Add(UserData.Empty());
		}).ConfigureAwait(false);

		// The host started before the migration found no token tables; register the sign-in clients now.
		await Api.Services.GetRequiredService<OpenIddictClientSeeder>().SeedAsync(CancellationToken.None).ConfigureAwait(false);
		_web = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseEnvironment("Development")
			.UseApiHost(Api));
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
		using var client = await SignedInAsync(Factory);

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

	/// <summary>Verifies the circuit's negotiation request is served to the signed-in owner, the way a browser's own
	/// page sends it.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task PostNegotiate_SignedInOwnerSameOrigin_Served()
	{
		using var client = await SignedInAsync(Factory);

		using var response = await RequestAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));

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
		using var client = await SignedInAsync(connected);

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
		using var client = await SignedInAsync(Factory);

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
		using var client = await SignedInAsync(Factory);
		using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/settings", UriKind.Relative));
		request.Headers.Add("Cookie", "kbc-notice=Bank%20connection%20failed");

		using var withNotice = await client.SendAsync(request);
		using var without = await client.GetAsync(new Uri("/settings", UriKind.Relative));

		withNotice.StatusCode.Should().Be(HttpStatusCode.OK);
		withNotice.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie => cookie.StartsWith("kbc-notice=;", StringComparison.Ordinal)
			&& cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
		(without.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : []).Should().NotContain(cookie => cookie.StartsWith("kbc-notice=", StringComparison.Ordinal));
	}
	#endregion

	#region Access Tests
	/// <summary>Verifies an anonymous page, the export and the bank callback send the browser to the sign-in page, which
	/// remembers where to return to.</summary>
	/// <param name="path">The address, one a person opens in the browser.</param>
	/// <param name="expectedReturn">The escaped return address the sign-in page is given.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/overview", "%2Foverview")]
	[InlineData("/export.csv", "%2Fexport.csv")]
	[InlineData("/callback?code=granted&state=known", "%2Fcallback%3Fcode%3Dgranted%26state%3Dknown")]
	[InlineData("/", "%2F")]
	public async Task Get_AnonymousNavigation_RedirectsToSignIn(string path, string expectedReturn)
	{
		using var client = Anonymous(Factory);

		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.AbsolutePath.Should().Be("/signin");
		response.Headers.Location.Query.Should().Be("?returnUrl=" + expectedReturn);
	}

	/// <summary>Verifies the circuit cannot be reached without signing in: its negotiation is answered with 401, not a
	/// redirect it could not follow, and a WebSocket upgrade is not let through.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Circuit_Anonymous_Unauthorized()
	{
		using var client = Anonymous(Factory);

		using var negotiate = await RequestAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));
		using var upgrade = await RequestAsync(client, HttpMethod.Get, new Uri("/_blazor", UriKind.Relative), request =>
		{
			request.Headers.Add("Connection", "Upgrade");
			request.Headers.Add("Upgrade", "websocket");
			request.Headers.Add("Sec-Fetch-Site", "same-origin");
		});

		negotiate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		upgrade.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>Verifies a signed-in account that is not the owner is refused everywhere: pages go back to the sign-in page and
	/// the circuit is answered with a status.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Request_SignedInNonOwner_Refused()
	{
		using var client = await SignedInAsync(Factory, "someone-else");

		using var page = await client.GetAsync(new Uri("/overview", UriKind.Relative));
		using var negotiate = await RequestAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));

		page.StatusCode.Should().Be(HttpStatusCode.Redirect);
		page.Headers.Location!.AbsolutePath.Should().Be("/signin");
		negotiate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>Verifies the signed-in owner passes on a page, on the circuit's negotiation and on the export.</summary>
	/// <param name="path">The address, one the circuit or a download uses.</param>
	/// <param name="method">The request method.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/overview", "GET")]
	[InlineData("/export.csv", "GET")]
	[InlineData("/_blazor/negotiate?negotiateVersion=1", "POST")]
	public async Task Request_SignedInOwner_Served(string path, string method)
	{
		using var client = await SignedInAsync(Factory);

		using var response = await RequestAsync(client, new HttpMethod(method), new Uri(path, UriKind.Relative), request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	/// <summary>Verifies the static assets the shell loads need no sign-in.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_AnonymousStaticAsset_Served()
	{
		using var client = Anonymous(Factory);

		using var response = await client.GetAsync(new Uri("/_framework/blazor.web.js", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	/// <summary>Verifies the sign-in page is static HTML (no circuit script) that offers the configured provider and carries
	/// the return address into the sign-in start.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_SignInPage_OffersConfiguredProviderStatically()
	{
		using var client = Anonymous(Factory);

		using var response = await client.GetAsync(new Uri("/signin?returnUrl=%2Fsettings", UriKind.Relative));
		var html = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
		html.Should().Contain("Sign in with Google").And.Contain("/signin/start?provider=Google&amp;returnUrl=%2Fsettings").And.NotContain("blazor.web");
	}

	/// <summary>Verifies the sign-in start refuses a provider the host does not offer, so the address cannot be used to probe the API.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_SignInStartUnknownProvider_BadRequest()
	{
		using var client = Anonymous(Factory);

		using var response = await client.GetAsync(new Uri("/signin/start?provider=Evil", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>Verifies a signed-in owner opening the sign-in page goes on to the page asked for, and that a return address
	/// that points off this site is replaced by the root.</summary>
	/// <param name="returnUrl">The requested return address.</param>
	/// <param name="expected">Where the owner is sent.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/settings", "/settings")]
	[InlineData("//evil.example", "/")]
	[InlineData("https://evil.example", "/")]
	[InlineData("/\\evil.example", "/")]
	public async Task Get_SignInPageSignedIn_RedirectsToLocalReturnUrl(string returnUrl, string expected)
	{
		using var client = await SignedInAsync(Factory);

		using var response = await client.GetAsync(new Uri("/signin?returnUrl=" + Uri.EscapeDataString(returnUrl), UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location!.OriginalString.Should().Be(expected);
	}

	/// <summary>Verifies the sign-out refuses a post without an antiforgery token, and with one ends the session: the cookie is
	/// cleared and the next page asks to sign in again.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task PostSignOut_AntiforgeryToken_EndsTheSession()
	{
		using var client = await SignedInAsync(Factory);
		var token = await client.GetStringAsync(new Uri(TestSignInStartupFilter.AntiforgeryRoute, UriKind.Relative));

		using var without = await RequestAsync(client, HttpMethod.Post, new Uri("/signout", UriKind.Relative), request => request.Headers.Add("Sec-Fetch-Site", "same-origin"));
		using var signedOut = await RequestAsync(client, HttpMethod.Post, new Uri("/signout", UriKind.Relative), request =>
		{
			request.Headers.Add("Sec-Fetch-Site", "same-origin");
			request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
		});
		using var afterwards = await client.GetAsync(new Uri("/overview", UriKind.Relative));

		without.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		signedOut.StatusCode.Should().Be(HttpStatusCode.Redirect);
		signedOut.Headers.Location!.OriginalString.Should().Be("/signin");
		signedOut.Headers.GetValues("Set-Cookie").Should().Contain(cookie => cookie.StartsWith(WebAuthentication.CookieName + "=;", StringComparison.Ordinal));
		afterwards.StatusCode.Should().Be(HttpStatusCode.Redirect);
		afterwards.Headers.Location!.AbsolutePath.Should().Be("/signin");
	}

	/// <summary>Verifies the session cookie is script-proof, HTTPS-only and same-site Lax.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SignIn_SessionCookie_HttpOnlySecureLax()
	{
		using var client = Anonymous(Factory);

		using var response = await client.GetAsync(new Uri(TestSignInStartupFilter.SignInRoute, UriKind.Relative));

		response.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie => cookie.StartsWith(WebAuthentication.CookieName + "=", StringComparison.Ordinal)
			&& cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && cookie.Contains("secure", StringComparison.OrdinalIgnoreCase)
			&& cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Verifies a write from another site is refused even from the signed-in owner (and from an anonymous caller),
	/// while the owner's own same-origin write (the circuit's negotiation, which a browser sends with
	/// <c>Sec-Fetch-Site: same-origin</c> and, under the no-referrer policy, <c>Origin: null</c>) and a navigation typed into
	/// the address bar pass. Another port on this PC and a forged origin without fetch metadata stay refused.</summary>
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
	public async Task PostNegotiate_SignedInOwnerFetchMetadata_OnlyOwnPageWrites(string site, string origin, HttpStatusCode expected)
	{
		using var client = await SignedInAsync(Factory);
		using var anonymous = Anonymous(Factory);

		using var response = await RequestAsync(client, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), request => AddFetchMetadata(request, site, origin));
		using var anonymousResponse = await RequestAsync(anonymous, HttpMethod.Post, new Uri(NegotiatePath, UriKind.Relative), request => AddFetchMetadata(request, site, origin));

		response.StatusCode.Should().Be(expected);
		anonymousResponse.StatusCode.Should().Be(expected == HttpStatusCode.Forbidden ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
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
	public async Task GetCircuitUpgrade_SignedInOwnerFetchMetadata_OnlyOwnPageConnects(string site, string origin, bool refused)
	{
		using var client = await SignedInAsync(Factory);

		using var response = await RequestAsync(client, HttpMethod.Get, new Uri("/_blazor", UriKind.Relative), request =>
		{
			request.Headers.Add("Connection", "Upgrade");
			request.Headers.Add("Upgrade", "websocket");
			AddFetchMetadata(request, site, origin);
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
		using var client = await SignedInAsync(Factory);

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
		using var client = await SignedInAsync(Factory);

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
		using var client = await SignedInAsync(unavailable);

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
		using var client = await SignedInAsync(refusing);

		using var response = await client.GetAsync(new Uri("/export.csv", UriKind.Relative));

		response.StatusCode.Should().Be(status);
		(await response.Content.ReadAsStringAsync()).Should().Contain(expected).And.NotContain("Backend unavailable");
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates a client of a web host that is not signed in, over HTTPS so the secure cookies would be sent, without following redirects.</summary>
	private static HttpClient Anonymous(WebApplicationFactory<Program> host) =>
		host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

	/// <summary>Creates a client of a web host that holds the session cookie of an account, through the test-only sign-in route.</summary>
	private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> host, string owner = WebAuthentication.OwnerId)
	{
		var client = Anonymous(host);
		using var response = await client.GetAsync(new Uri($"{TestSignInStartupFilter.SignInRoute}?owner={Uri.EscapeDataString(owner)}", UriKind.Relative)).ConfigureAwait(false);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		return client;
	}

	/// <summary>Adds the browser's fetch metadata headers a test names; an empty value adds nothing.</summary>
	private static void AddFetchMetadata(HttpRequestMessage request, string site, string origin)
	{
		if (site.Length > 0)
		{
			request.Headers.Add("Sec-Fetch-Site", site);
		}

		if (origin.Length > 0)
		{
			request.Headers.Add("Origin", origin);
		}
	}

	/// <summary>Starts a web host whose backend answers one route with a problem and forwards the rest to the real API.</summary>
	private WebApplicationFactory<Program> WithRefusingBackend(string route, HttpStatusCode status, string detail) =>
		Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddHttpClient<BackendClient>()
			.ConfigurePrimaryHttpMessageHandler(() => new RefusingBackendHandler(route, status, detail) { InnerHandler = Api.Server.CreateHandler() })));

	/// <summary>Sends a request the test can adjust; a POST carries an empty body unless the adjustment sets one.</summary>
	private static async Task<HttpResponseMessage> RequestAsync(HttpClient client, HttpMethod method, Uri path, Action<HttpRequestMessage>? configure = null)
	{
		using var request = new HttpRequestMessage(method, path);
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
