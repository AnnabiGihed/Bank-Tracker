using System.Net;
using System.Security.Claims;
using KbcTracker.Web.Features.Authentication;

namespace KbcTracker.E2E.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Checks the web host's token client against a stub token endpoint: that the client-credentials token is
///              cached and replaced shortly before it expires, that a failing endpoint is reported, that the refresh token
///              is revoked with the client's credentials, and when a circuit's session still counts as the owner's.
/// </summary>
public sealed class ApiTokenClientTests
{
	#region Constants
	/// <summary>The secret the stub expects.</summary>
	private const string Secret = "stub-secret";
	#endregion

	#region Token Tests
	/// <summary>Verifies a fresh token is reused and a token near its expiry is replaced.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_NearExpiry_FetchesANewTokenOnlyThen()
	{
		var clock = new StubClock(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero));
		using var handler = new StubTokenHandler();
		using var client = new ApiTokenClient(new StubFactory(handler), new WebClientSecret(Secret), clock);

		var first = await client.GetAccessTokenAsync(CancellationToken.None);
		clock.Advance(TimeSpan.FromSeconds(500));
		var second = await client.GetAccessTokenAsync(CancellationToken.None);
		clock.Advance(TimeSpan.FromSeconds(50));
		var third = await client.GetAccessTokenAsync(CancellationToken.None);

		first.Should().Be("token-1");
		second.Should().Be("token-1", "540 of 600 seconds remain usable");
		third.Should().Be("token-2", "the token is replaced 60 seconds before it expires");
		handler.Requests.Should().HaveCount(2);
		handler.Requests[0].Should().Contain("grant_type=client_credentials").And.Contain("client_id=web").And.Contain("client_secret=" + Secret);
	}

	/// <summary>Verifies a refused token request surfaces as an HTTP failure, which the web host maps to a safe problem response.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task GetAccessToken_EndpointRefuses_ThrowsHttpRequestException()
	{
		using var handler = new StubTokenHandler { Status = HttpStatusCode.BadRequest };
		using var client = new ApiTokenClient(new StubFactory(handler), new WebClientSecret(Secret), TimeProvider.System);

		var act = () => client.GetAccessTokenAsync(CancellationToken.None);

		await act.Should().ThrowAsync<HttpRequestException>();
	}

	/// <summary>Verifies the refresh token is revoked at the API's revocation endpoint with the web client's credentials.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RevokeRefreshToken_Posts_TokenAndClientCredentials()
	{
		using var handler = new StubTokenHandler();
		using var client = new ApiTokenClient(new StubFactory(handler), new WebClientSecret(Secret), TimeProvider.System);

		await client.RevokeRefreshTokenAsync("refresh-abc", CancellationToken.None);

		handler.Paths.Should().Equal("/connect/revoke");
		handler.Requests.Should().ContainSingle().Which.Should().Contain("token=refresh-abc").And.Contain("token_type_hint=refresh_token").And.Contain("client_id=web").And.Contain("client_secret=" + Secret);
	}

	/// <summary>Verifies the secret never appears in the text form of its holder.</summary>
	[Fact]
	public void ToString_WebClientSecret_HidesTheSecret() => new WebClientSecret(Secret).ToString().Should().NotContain(Secret);
	#endregion

	#region Session Tests
	/// <summary>Verifies a circuit's principal counts as the owner's only while it is authenticated, carries the owner claim and its session has not ended.</summary>
	/// <param name="owner">The value of the owner claim, or empty for none.</param>
	/// <param name="secondsLeft">Seconds until the session ends; negative once it has ended.</param>
	/// <param name="expected">Whether the session is still open.</param>
	[Theory]
	[InlineData("owner", 60, true)]
	[InlineData("owner", -1, false)]
	[InlineData("someone-else", 60, false)]
	[InlineData("", 60, false)]
	public void IsOpen_OwnerClaimAndSessionEnd_DecidesTheCircuit(string owner, int secondsLeft, bool expected)
	{
		var now = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
		var claims = new List<Claim> { new(WebAuthentication.SessionEndClaim, now.AddSeconds(secondsLeft).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)) };
		if (owner.Length > 0)
		{
			claims.Add(new Claim(WebAuthentication.OwnerClaim, owner));
		}

		OwnerAuthenticationStateProvider.IsOpen(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), now).Should().Be(expected);
		OwnerAuthenticationStateProvider.IsOpen(new ClaimsPrincipal(new ClaimsIdentity()), now).Should().BeFalse("an anonymous principal is never the owner");
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : A clock the test moves by hand.
	/// </summary>
	/// <param name="start">The first reading.</param>
	private sealed class StubClock(DateTimeOffset start) : TimeProvider
	{
		#region Fields
		/// <summary>The current reading.</summary>
		private DateTimeOffset _now = start;
		#endregion

		#region Public Methods
		/// <summary>Moves the clock forward.</summary>
		/// <param name="by">How far to move it.</param>
		public void Advance(TimeSpan by) => _now += by;

		/// <inheritdoc />
		public override DateTimeOffset GetUtcNow() => _now;
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers the token and revocation endpoints like the API and records what it was sent.
	/// </summary>
	private sealed class StubTokenHandler : HttpMessageHandler
	{
		#region Properties
		/// <summary>Gets the request bodies received, in order.</summary>
		public List<string> Requests { get; } = [];

		/// <summary>Gets the paths requested, in order.</summary>
		public List<string> Paths { get; } = [];

		/// <summary>Gets or sets the status to answer with.</summary>
		public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Paths.Add(request.RequestUri!.AbsolutePath);
			Requests.Add(Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
			return new HttpResponseMessage(Status)
			{
				Content = new StringContent($$"""{"access_token":"token-{{Requests.Count}}","expires_in":600}""", System.Text.Encoding.UTF8, "application/json"),
			};
		}
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Hands out a client that talks to the stub, with the API's base address.
	/// </summary>
	/// <param name="handler">The stub endpoint.</param>
	private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		#region IHttpClientFactory Implementation
		/// <inheritdoc />
		public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/") };
		#endregion
	}
	#endregion
}
