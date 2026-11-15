using System.Net;
using System.Text;

namespace KbcTracker.Shared.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the Android app's token logic without Android: the authorize address, the code exchange, keeping
///              only the refresh token, refreshing shortly before expiry (and once, however many callers), the end of the
///              session when the API refuses the refresh token (no retry), and the revocation at sign-out.
/// </summary>
public sealed class PublicClientTokenServiceTests : IDisposable
{
	#region Constants
	/// <summary>The API's address in the tests.</summary>
	private static readonly Uri Authority = new("http://localhost:5080/");

	/// <summary>The redirect address of the app.</summary>
	private static readonly Uri Redirect = new("com.banktracker.mobile://auth");
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers the token service's requests from a script and records each request's path and form.
	/// </summary>
	private sealed class ScriptedHandler : HttpMessageHandler
	{
		#region Fields
		/// <summary>The answers still to give, in order.</summary>
		private readonly Queue<Func<HttpResponseMessage>> _answers = new();
		#endregion

		#region Properties
		/// <summary>Gets the requests received, as path and form body.</summary>
		public List<(string Path, string Body)> Requests { get; } = [];
		#endregion

		#region Public Methods
		/// <summary>Adds a JSON answer.</summary>
		/// <param name="status">The status.</param>
		/// <param name="json">The body.</param>
		public void Answer(HttpStatusCode status, string json) =>
			_answers.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

		/// <summary>Adds an answer that is a failure to reach the API.</summary>
		public void Unreachable() => _answers.Enqueue(() => throw new HttpRequestException("unreachable"));
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add((request.RequestUri!.AbsolutePath, await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
			return _answers.Dequeue()();
		}
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : A clock the test moves by hand.
	/// </summary>
	private sealed class ManualClock : TimeProvider
	{
		#region Fields
		/// <summary>The current time.</summary>
		private DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
		#endregion

		#region Public Methods
		/// <summary>Moves the clock forward.</summary>
		/// <param name="by">How far.</param>
		public void Advance(TimeSpan by) => _now += by;
		#endregion

		#region Overrides
		/// <inheritdoc />
		public override DateTimeOffset GetUtcNow() => _now;
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Keeps the refresh token in memory, in place of the platform's secure storage.
	/// </summary>
	private sealed class MemoryStore : IRefreshTokenStore
	{
		#region Properties
		/// <summary>Gets or sets the stored token.</summary>
		public string? Token { get; set; }
		#endregion

		#region IRefreshTokenStore Implementation
		/// <inheritdoc />
		public Task<string?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Token);

		/// <inheritdoc />
		public Task SetAsync(string refreshToken, CancellationToken cancellationToken)
		{
			Token = refreshToken;
			return Task.CompletedTask;
		}

		/// <inheritdoc />
		public Task ClearAsync(CancellationToken cancellationToken)
		{
			Token = null;
			return Task.CompletedTask;
		}
		#endregion
	}
	#endregion

	#region Fields
	/// <summary>The scripted API.</summary>
	private readonly ScriptedHandler _api = new();

	/// <summary>The clock.</summary>
	private readonly ManualClock _clock = new();

	/// <summary>The refresh token store.</summary>
	private readonly MemoryStore _store = new();
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => _api.Dispose();
	#endregion

	#region Authorize Address Tests
	/// <summary>Verifies the authorize address carries the code flow parameters, the S256 challenge and the provider hint.</summary>
	[Fact]
	public void BuildAuthorizeUri_CodeFlowWithPkceAndProvider()
	{
		using var service = CreateService();
		var proof = new PkceChallenge("verifier", "the-challenge");

		var uri = service.BuildAuthorizeUri(proof, "state-1", "Google");

		uri.GetLeftPart(UriPartial.Path).Should().Be("http://localhost:5080/connect/authorize");
		uri.Query.Should().Contain("client_id=mobile")
			.And.Contain("response_type=code")
			.And.Contain("redirect_uri=com.banktracker.mobile%3A%2F%2Fauth")
			.And.Contain("scope=openid%20offline_access")
			.And.Contain("state=state-1")
			.And.Contain("code_challenge=the-challenge")
			.And.Contain("code_challenge_method=S256")
			.And.Contain("provider=Google")
			.And.NotContain("verifier");
	}

	/// <summary>Verifies no provider parameter is sent when none is chosen.</summary>
	[Fact]
	public void BuildAuthorizeUri_NoProvider_OmitsTheParameter()
	{
		using var service = CreateService();

		var uri = service.BuildAuthorizeUri(PkceChallenge.Create(), "s", provider: null);

		uri.Query.Should().NotContain("provider=");
	}
	#endregion

	#region Sign-In Tests
	/// <summary>Verifies the code exchange sends the verifier and redirect, and keeps only the refresh token in the store.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SignIn_ValidCode_StoresRefreshTokenAndServesAccessToken()
	{
		_api.Answer(HttpStatusCode.OK, Tokens("access-1", "refresh-1", 600));
		using var service = CreateService();

		await service.SignInAsync("the-code", "the-verifier", CancellationToken.None);
		var token = await service.GetAccessTokenAsync(CancellationToken.None);

		token.Should().Be("access-1");
		_store.Token.Should().Be("refresh-1");
		_api.Requests.Should().ContainSingle();
		_api.Requests[0].Path.Should().Be("/connect/token");
		_api.Requests[0].Body.Should().Contain("grant_type=authorization_code").And.Contain("code=the-code")
			.And.Contain("code_verifier=the-verifier").And.Contain("client_id=mobile").And.Contain("redirect_uri=");
	}

	/// <summary>Verifies a refused code surfaces as an error and leaves no session.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SignIn_RefusedCode_ThrowsAndStoresNothing()
	{
		_api.Answer(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
		using var service = CreateService();

		var act = () => service.SignInAsync("bad", "verifier", CancellationToken.None);

		await act.Should().ThrowAsync<HttpRequestException>();
		_store.Token.Should().BeNull();
	}
	#endregion

	#region Refresh Tests
	/// <summary>Verifies no token and no request exist while nobody is signed in.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_NoSession_NullWithoutRequest()
	{
		using var service = CreateService();

		var token = await service.GetAccessTokenAsync(CancellationToken.None);

		token.Should().BeNull();
		_api.Requests.Should().BeEmpty();
	}

	/// <summary>Verifies a fresh token is reused, and a token within a minute of its expiry is replaced by a refresh that
	/// rotates the stored refresh token.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_NearExpiry_RefreshesAndRotates()
	{
		_api.Answer(HttpStatusCode.OK, Tokens("access-1", "refresh-1", 600));
		_api.Answer(HttpStatusCode.OK, Tokens("access-2", "refresh-2", 600));
		using var service = CreateService();
		await service.SignInAsync("code", "verifier", CancellationToken.None);

		_clock.Advance(TimeSpan.FromSeconds(539));
		var stillFresh = await service.GetAccessTokenAsync(CancellationToken.None);
		_clock.Advance(TimeSpan.FromSeconds(2));
		var refreshed = await service.GetAccessTokenAsync(CancellationToken.None);

		stillFresh.Should().Be("access-1");
		refreshed.Should().Be("access-2");
		_store.Token.Should().Be("refresh-2");
		_api.Requests.Should().HaveCount(2);
		_api.Requests[1].Body.Should().Contain("grant_type=refresh_token").And.Contain("refresh_token=refresh-1");
	}

	/// <summary>Verifies a token that lives less than two minutes is refreshed halfway through its life.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_ShortLifetime_RefreshesAtHalf()
	{
		_api.Answer(HttpStatusCode.OK, Tokens("access-1", "refresh-1", 100));
		_api.Answer(HttpStatusCode.OK, Tokens("access-2", "refresh-2", 100));
		using var service = CreateService();
		await service.SignInAsync("code", "verifier", CancellationToken.None);

		_clock.Advance(TimeSpan.FromSeconds(51));
		var token = await service.GetAccessTokenAsync(CancellationToken.None);

		token.Should().Be("access-2");
	}

	/// <summary>Verifies concurrent callers share one refresh, because the refresh token can be redeemed once.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_ConcurrentCallers_OneRefresh()
	{
		_store.Token = "refresh-0";
		_api.Answer(HttpStatusCode.OK, Tokens("access-1", "refresh-1", 600));
		using var service = CreateService();

		var tokens = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.GetAccessTokenAsync(CancellationToken.None)));

		tokens.Should().OnlyContain(token => token == "access-1");
		_api.Requests.Should().ContainSingle();
	}

	/// <summary>Verifies a refresh token the API no longer accepts ends the session once: the store is cleared, the event is
	/// raised, and nothing is retried.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_RefreshTokenRefused_EndsSessionWithoutRetry()
	{
		_store.Token = "revoked";
		_api.Answer(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"gone"}""");
		using var service = CreateService();
		var ended = 0;
		service.SessionEnded += (_, _) => ended++;

		var first = await service.GetAccessTokenAsync(CancellationToken.None);
		var second = await service.GetAccessTokenAsync(CancellationToken.None);

		first.Should().BeNull();
		second.Should().BeNull();
		_store.Token.Should().BeNull();
		ended.Should().Be(1);
		_api.Requests.Should().ContainSingle();
	}

	/// <summary>Verifies an unreachable API or a server fault keeps the session for the next try.</summary>
	/// <param name="serverFault">Whether the API answers 503 instead of not answering.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GetAccessToken_ApiDown_ThrowsAndKeepsRefreshToken(bool serverFault)
	{
		_store.Token = "refresh-0";
		if (serverFault)
		{
			_api.Answer(HttpStatusCode.ServiceUnavailable, "down");
		}
		else
		{
			_api.Unreachable();
		}

		using var service = CreateService();
		var ended = false;
		service.SessionEnded += (_, _) => ended = true;

		var act = () => service.GetAccessTokenAsync(CancellationToken.None);

		await act.Should().ThrowAsync<HttpRequestException>();
		_store.Token.Should().Be("refresh-0");
		ended.Should().BeFalse();
	}
	#endregion

	#region Sign-Out Tests
	/// <summary>Verifies sign-out revokes the refresh token at the API and clears the store and the access token.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SignOut_RevokesAndClears()
	{
		_api.Answer(HttpStatusCode.OK, Tokens("access-1", "refresh-1", 600));
		_api.Answer(HttpStatusCode.OK, "{}");
		using var service = CreateService();
		await service.SignInAsync("code", "verifier", CancellationToken.None);

		var revoked = await service.SignOutAsync(CancellationToken.None);
		var after = await service.GetAccessTokenAsync(CancellationToken.None);

		revoked.Should().BeTrue();
		_store.Token.Should().BeNull();
		after.Should().BeNull();
		_api.Requests[1].Path.Should().Be("/connect/revoke");
		_api.Requests[1].Body.Should().Contain("token=refresh-1").And.Contain("token_type_hint=refresh_token").And.Contain("client_id=mobile");
	}

	/// <summary>Verifies sign-out forgets the tokens locally even when the API cannot be reached, and says it could not revoke.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SignOut_ApiUnreachable_ClearsAndReportsNotRevoked()
	{
		_store.Token = "refresh-1";
		_api.Unreachable();
		using var service = CreateService();

		var revoked = await service.SignOutAsync(CancellationToken.None);

		revoked.Should().BeFalse();
		_store.Token.Should().BeNull();
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates the service over the scripted API.</summary>
	private PublicClientTokenService CreateService()
	{
		var factory = Substitute.For<IHttpClientFactory>();
		factory.CreateClient(PublicClientTokenService.HttpClientName).Returns(_ => new HttpClient(_api, disposeHandler: false) { BaseAddress = Authority });
		return new PublicClientTokenService(factory, new PublicClientOptions(Authority, "mobile", Redirect), _store, _clock);
	}

	/// <summary>Builds a token endpoint response body.</summary>
	private static string Tokens(string access, string refresh, int seconds) =>
		$$"""{"access_token":"{{access}}","refresh_token":"{{refresh}}","token_type":"Bearer","expires_in":{{seconds}}}""";
	#endregion
}
