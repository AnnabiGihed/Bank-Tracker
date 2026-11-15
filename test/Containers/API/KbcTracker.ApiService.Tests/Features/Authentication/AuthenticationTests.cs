using System.Net;
using System.Net.Http.Headers;
using KbcTracker.ApiService.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the owner's sign-in end to end against the real API, token service and PostgreSQL database:
///              what gets in (the web client's token, the owner's user token), what stays out (no token, a malformed,
///              expired or wrong-audience one, a user who is not the owner) and how the clients and keys are set up.
///              The external provider is replaced only by a test-only cookie; every token is a real OpenIddict token.
/// </summary>
/// <param name="fixture">The shared isolated backend.</param>
public sealed class AuthenticationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Constants
	/// <summary>The web app's registered redirect address.</summary>
	private const string WebRedirect = "https://localhost:5001/signin-oidc";

	/// <summary>The audience a token for some other API would carry.</summary>
	private const string OtherAudience = "other-api";
	#endregion

	#region Access Tests
	/// <summary>Verifies every data endpoint answers 401 to a caller with no token.</summary>
	/// <param name="path">The endpoint.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("/api/v1/connection")]
	[InlineData("/api/v1/transactions")]
	[InlineData("/api/v1/workspace")]
	[InlineData("/api/v1/banks")]
	[InlineData("/api/v1/consent/callback?code=x&state=y")]
	public async Task Get_NoToken_Unauthorized(string path)
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = factory.CreateClient();

		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>Verifies health is the one data-free endpoint that needs no token.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_Health_AnonymousAllowed()
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = factory.CreateClient();

		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	/// <summary>Verifies a token the API did not issue is refused.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_ForeignToken_Unauthorized()
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJSUzI1NiJ9.e30.not-issued-here");

		using var response = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>Verifies the web client's client-credentials token is accepted, outside Development too, and Swagger is not published there.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_WebClientToken_OkInProductionWithoutSwagger()
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = ApiFixture.CreateAuthorizedClient(factory);

		using var allowed = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));
		using var swagger = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

		allowed.StatusCode.Should().Be(HttpStatusCode.OK);
		swagger.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>Verifies a token past its lifetime is refused.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_ExpiredToken_Unauthorized()
	{
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:AccessTokenSeconds", "1"));
		var token = await WebClientTokenHandler.FetchAsync(factory, CancellationToken.None);
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		using var fresh = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));

		await Task.Delay(TimeSpan.FromSeconds(2));
		using var expired = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));

		fresh.StatusCode.Should().Be(HttpStatusCode.OK);
		expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>Verifies a genuine token that was issued for another audience is refused.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_WrongAudienceToken_Unauthorized()
	{
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.ConfigureTestServices(services =>
			services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
				.UseInlineHandler(context =>
				{
					if (context.Request?.GetParameter("test_audience") is { } audience && context.AccessTokenPrincipal is not null)
					{
						context.AccessTokenPrincipal.SetAudiences((string?)audience ?? OtherAudience);
					}

					return default;
				})
				.SetOrder(OpenIddictServerHandlers.PrepareAccessTokenPrincipal.Descriptor.Order + 1)))));
		var token = await WebClientTokenHandler.FetchAsync(factory, CancellationToken.None, ("test_audience", OtherAudience));
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		using var response = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
	#endregion

	#region User Token Tests
	/// <summary>Verifies the owner's user token, got through the real authorization code flow with PKCE, is accepted, for the
	/// confidential web client and the public mobile client alike.</summary>
	/// <param name="clientId">The client.</param>
	/// <param name="secret">The client's secret, or <see langword="null"/> when it has none.</param>
	/// <param name="redirect">The client's registered redirect address.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("web", ApiFixture.WebSecret, WebRedirect)]
	[InlineData("mobile", null, "com.banktracker.mobile://auth")]
	public async Task Get_OwnerUserToken_Ok(string clientId, string? secret, string redirect)
	{
		using var factory = fixture.CreateFactory("Production");
		var flow = new AuthorizationCodeFlow(factory);
		await flow.SignInAsync("owner-account", owner: true);

		var (status, tokens) = await flow.ExchangeAsync(clientId, secret, redirect);

		status.Should().Be(HttpStatusCode.OK);
		tokens.TryGetProperty("refresh_token", out _).Should().BeTrue("offline_access was requested");
		(await flow.GetAsync(tokens.GetProperty("access_token").GetString()!, "/api/v1/connection")).Should().Be(HttpStatusCode.OK);
	}

	/// <summary>Verifies a genuine user token whose subject is not the owner gets 403: signed in, but not allowed.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Get_NonOwnerUserToken_Forbidden()
	{
		using var factory = fixture.CreateFactory("Production");
		var flow = new AuthorizationCodeFlow(factory);
		await flow.SignInAsync("someone-else", owner: false);

		var (status, tokens) = await flow.ExchangeAsync("web", ApiFixture.WebSecret, WebRedirect);

		status.Should().Be(HttpStatusCode.OK);
		(await flow.GetAsync(tokens.GetProperty("access_token").GetString()!, "/api/v1/connection")).Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>Verifies a refresh token is single-use: redeeming it returns a new pair, and the old one then stops working.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Refresh_Rotation_OldRefreshTokenRefused()
	{
		using var factory = fixture.CreateFactory("Production");
		var flow = new AuthorizationCodeFlow(factory);
		await flow.SignInAsync("owner-account", owner: true);
		var (_, first) = await flow.ExchangeAsync("mobile", secret: null, "com.banktracker.mobile://auth");
		var original = first.GetProperty("refresh_token").GetString()!;

		var (rotatedStatus, rotated) = await flow.RefreshAsync("mobile", secret: null, original);
		var (reuseStatus, _) = await flow.RefreshAsync("mobile", secret: null, original);

		rotatedStatus.Should().Be(HttpStatusCode.OK);
		rotated.GetProperty("refresh_token").GetString().Should().NotBe(original);
		(await flow.GetAsync(rotated.GetProperty("access_token").GetString()!, "/api/v1/connection")).Should().Be(HttpStatusCode.OK);
		reuseStatus.Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>Verifies a refresh token is refused once the provider account is no longer on the allow-list, and that the
	/// refusal revokes the authorization, so the token stays dead even where the account is allowed again.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Refresh_AccountNoLongerAllowed_InvalidGrantAndAuthorizationRevoked()
	{
		const string subject = "removed-owner-subject";
		using var allowing = fixture.CreateFactory("Production");
		var flow = new AuthorizationCodeFlow(allowing);
		await flow.SignInAsync(subject, owner: true);
		var (_, issued) = await flow.ExchangeAsync("mobile", secret: null, "com.banktracker.mobile://auth");
		var refreshToken = issued.GetProperty("refresh_token").GetString()!;
		using var removed = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:Owners:0:Subject", "somebody-else"));

		var (refusedStatus, refused) = await new AuthorizationCodeFlow(removed).RefreshAsync("mobile", secret: null, refreshToken);
		var (laterStatus, _) = await flow.RefreshAsync("mobile", secret: null, refreshToken);

		refusedStatus.Should().Be(HttpStatusCode.BadRequest);
		refused.GetProperty("error").GetString().Should().Be("invalid_grant");
		laterStatus.Should().Be(HttpStatusCode.BadRequest);
		var scope = allowing.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
			var revoked = 0;
			await foreach (var authorization in authorizations.FindBySubjectAsync(subject))
			{
				(await authorizations.HasStatusAsync(authorization, Statuses.Revoked)).Should().BeTrue();
				revoked++;
			}

			revoked.Should().BeGreaterThan(0);
		}
	}

	/// <summary>Verifies a client that leaves out the PKCE challenge, or a web client that sends a wrong secret, gets no token.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Exchange_NoPkceOrWrongSecret_Refused()
	{
		using var factory = fixture.CreateFactory("Production");
		var flow = new AuthorizationCodeFlow(factory);
		await flow.SignInAsync("owner-account", owner: true);

		var (noPkceStatus, withoutPkce) = await flow.ExchangeAsync("mobile", secret: null, "com.banktracker.mobile://auth", usePkce: false);
		var (wrongSecretStatus, _) = await flow.ExchangeAsync("web", "not-the-secret", WebRedirect);

		noPkceStatus.Should().Be(HttpStatusCode.BadRequest);
		withoutPkce.GetProperty("error").GetString().Should().Be("invalid_request");
		wrongSecretStatus.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>Verifies an owner who is not signed in is sent to the provider, which is a clear problem when none is configured.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Authorize_NotSignedInNoProvider_ServiceUnavailableWithMessage()
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		using var response = await client.GetAsync(new Uri(
			$"/connect/authorize?client_id=web&redirect_uri={Uri.EscapeDataString(WebRedirect)}&response_type=code&scope=openid&code_challenge=abc&code_challenge_method=S256",
			UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
		(await response.Content.ReadAsStringAsync()).Should().Contain("No sign-in provider is configured");
	}
	#endregion

	#region Client And Key Tests
	/// <summary>Verifies the two clients are registered, once, however often the backend starts, and that extra redirect
	/// addresses of the web client are accepted.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Seed_RepeatedStarts_TwoClientsWithConfiguredRedirects()
	{
		const string extra = "https://example.invalid/signin-oidc";
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:Clients:Web:ExtraRedirectUris:0", extra));
		var seeder = factory.Services.GetRequiredService<OpenIddictClientSeeder>();

		await seeder.SeedAsync(CancellationToken.None);
		await seeder.SeedAsync(CancellationToken.None);

		var scope = factory.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
			(await manager.CountAsync()).Should().Be(2);
			var web = await manager.FindByClientIdAsync("web") ?? throw new InvalidOperationException("web is not registered.");
			(await manager.GetRedirectUrisAsync(web)).Should().BeEquivalentTo(WebRedirect, extra);
			(await manager.HasClientTypeAsync(web, ClientTypes.Confidential)).Should().BeTrue();
			var mobile = await manager.FindByClientIdAsync("mobile") ?? throw new InvalidOperationException("mobile is not registered.");
			(await manager.HasClientTypeAsync(mobile, ClientTypes.Public)).Should().BeTrue();
			(await manager.GetRedirectUrisAsync(mobile)).Should().Equal("com.banktracker.mobile://auth");
		}
	}

	/// <summary>Verifies an extra redirect address that is neither https nor http on a loopback host stops the backend at
	/// start, naming the setting.</summary>
	/// <param name="address">The configured address.</param>
	[Theory]
	[InlineData("http://example.invalid/signin-oidc")]
	[InlineData("ftp://localhost/signin-oidc")]
	[InlineData("not-an-address")]
	public void Start_InsecureExtraRedirectUri_NamesTheSetting(string address)
	{
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:Clients:Web:ExtraRedirectUris:0", address));

		var act = () => factory.Services;

		act.Should().Throw<InvalidOperationException>().WithMessage("*Authentication:Clients:Web:ExtraRedirectUris*");
	}

	/// <summary>Verifies https addresses and plain http on loopback hosts are accepted as extra redirect addresses.</summary>
	/// <param name="address">The configured address.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("https://example.invalid/signin-oidc")]
	[InlineData("http://localhost:5002/signin-oidc")]
	[InlineData("http://127.0.0.1:5002/signin-oidc")]
	[InlineData("http://[::1]:5002/signin-oidc")]
	public async Task Seed_SecureExtraRedirectUri_Accepted(string address)
	{
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:Clients:Web:ExtraRedirectUris:0", address));

		var act = () => factory.Services.GetRequiredService<OpenIddictClientSeeder>().SeedAsync(CancellationToken.None);

		await act.Should().NotThrowAsync();
	}

	/// <summary>Verifies a host outside Development refuses to start without persisted token keys, which would otherwise
	/// invalidate every token at each restart, and that a missing web client secret is named.</summary>
	[Fact]
	public void AddOwnerAuthentication_ProductionWithoutKeys_Throws()
	{
		var environment = Substitute.For<IHostEnvironment>();
		environment.EnvironmentName.Returns(Environments.Production);
		var configuration = new ConfigurationBuilder().Build();

		var act = () => new ServiceCollection().AddOwnerAuthentication(configuration, environment);

		act.Should().Throw<InvalidOperationException>().WithMessage("*Authentication:Signing:Path*Authentication:Encryption:Path*");
	}

	/// <summary>Verifies a missing web client secret stops the backend with a message that names the setting.</summary>
	[Fact]
	public void Start_NoWebClientSecret_NamesTheSetting()
	{
		using var factory = fixture.CreateFactory("Production", configure: builder => builder.UseSetting("Authentication:Clients:Web:Secret", string.Empty));

		var act = () => factory.Services;

		act.Should().Throw<InvalidOperationException>().WithMessage("*Authentication:Clients:Web:Secret*");
	}
	#endregion
}
