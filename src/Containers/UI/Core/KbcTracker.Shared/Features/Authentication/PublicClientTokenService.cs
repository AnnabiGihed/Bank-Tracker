using System.Text.Json;

namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The token logic of a public client (the Android app) against the API's token service (ADR-0009), free of any
///              platform: it builds the authorize address (code flow with PKCE), redeems the code, keeps the access token
///              in memory only and the rotating refresh token in an <see cref="IRefreshTokenStore"/>, refreshes the access
///              token shortly before it expires, and revokes the refresh token at sign-out. A refresh the API refuses ends
///              the session once (it clears the store and raises <see cref="SessionEnded"/>); it is never retried.
/// </summary>
/// <param name="httpClients">Creates the HTTP client of <see cref="HttpClientName"/>, whose base address is the API.</param>
/// <param name="options">Which client this is and where the API is.</param>
/// <param name="store">Keeps the refresh token between runs.</param>
/// <param name="clock">The clock the expiry is measured with.</param>
public sealed class PublicClientTokenService(IHttpClientFactory httpClients, PublicClientOptions options, IRefreshTokenStore store, TimeProvider clock) : IDisposable
{
	#region Constants
	/// <summary>The name of the HTTP client that reaches the API's token endpoints; it must not carry the access token handler.</summary>
	public const string HttpClientName = "kbc-api-token";

	/// <summary>The scopes asked for: the ID token scope and the refresh token.</summary>
	private const string Scopes = "openid offline_access";

	/// <summary>The OAuth error that says a refresh token is no longer valid.</summary>
	private const string InvalidGrant = "invalid_grant";
	#endregion

	#region Static Instances
	/// <summary>How long before its expiry the access token is replaced.</summary>
	private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
	#endregion

	#region Fields
	/// <summary>Lets one caller at a time use the refresh token, which rotates and so can be redeemed once.</summary>
	private readonly SemaphoreSlim _gate = new(1, 1);

	/// <summary>The access token with its refresh time, or <see langword="null"/> before the first one and after sign-out; one object swapped by reference, so a reader never sees a token with another token's time.</summary>
	private volatile AccessGrant? _grant;
	#endregion

	#region Events
	/// <summary>Raised when the API refused the refresh token, so the owner has to sign in again.</summary>
	public event EventHandler? SessionEnded;
	#endregion

	#region Public Methods
	/// <summary>Builds the address the system browser opens to sign in.</summary>
	/// <param name="proof">The proof key of this sign-in.</param>
	/// <param name="state">A random value the callback must return unchanged.</param>
	/// <param name="provider">The provider to sign in with, or <see langword="null"/> to let the API choose.</param>
	/// <returns>The authorize address.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="proof"/> is <see langword="null"/>.</exception>
	public Uri BuildAuthorizeUri(PkceChallenge proof, string state, string? provider)
	{
		ArgumentNullException.ThrowIfNull(proof);
		var query = new List<KeyValuePair<string, string>>
		{
			new("client_id", options.ClientId),
			new("redirect_uri", options.RedirectUri.OriginalString),
			new("response_type", "code"),
			new("scope", Scopes),
			new("state", state),
			new("code_challenge", proof.Challenge),
			new("code_challenge_method", "S256"),
		};
		if (!string.IsNullOrWhiteSpace(provider))
		{
			query.Add(new("provider", provider));
		}

		return new Uri(options.Authority, "connect/authorize?" + string.Join('&', query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
	}

	/// <summary>Redeems the authorization code and starts the session.</summary>
	/// <param name="code">The code the browser returned.</param>
	/// <param name="verifier">The verifier of the proof key that made the request.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>A task that completes when the tokens are stored.</returns>
	/// <exception cref="HttpRequestException">The API refused the code or did not answer.</exception>
	/// <exception cref="InvalidOperationException">The token response is not valid.</exception>
	public async Task SignInAsync(string code, string verifier, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(code);
		ArgumentException.ThrowIfNullOrEmpty(verifier);
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			using var response = await PostAsync("connect/token", new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["grant_type"] = "authorization_code",
				["code"] = code,
				["redirect_uri"] = options.RedirectUri.OriginalString,
				["code_verifier"] = verifier,
			}, cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			await AcceptAsync(response, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Gets a valid access token, refreshing it when it is about to expire.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The access token, or <see langword="null"/> when nobody is signed in or the API ended the session.</returns>
	/// <exception cref="HttpRequestException">The API did not answer or failed; the session is kept for the next try.</exception>
	/// <exception cref="InvalidOperationException">The token response is not valid.</exception>
	public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		if (_grant is { } cached && IsFresh(cached))
		{
			return cached.Token;
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return _grant is { } current && IsFresh(current) ? current.Token : await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Ends the session: asks the API to revoke the refresh token, then forgets every token even when the API cannot be reached.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns><see langword="true"/> when nothing was left to revoke or the API revoked it; <see langword="false"/> when the API could not be asked, so the token lives on until it expires.</returns>
	public async Task<bool> SignOutAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var revoked = true;
			if (await store.GetAsync(cancellationToken).ConfigureAwait(false) is { } refreshToken)
			{
				revoked = await TryRevokeAsync(refreshToken, cancellationToken).ConfigureAwait(false);
			}

			await store.ClearAsync(cancellationToken).ConfigureAwait(false);
			_grant = null;
			return revoked;
		}
		finally
		{
			_gate.Release();
		}
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => _gate.Dispose();
	#endregion

	#region Private Helpers
	/// <summary>Whether the access token can still be used.</summary>
	private bool IsFresh(AccessGrant grant) => clock.GetUtcNow() < grant.RefreshAt;

	/// <summary>Redeems the stored refresh token. The caller holds the gate.</summary>
	private async Task<string?> RefreshAsync(CancellationToken cancellationToken)
	{
		_grant = null;
		if (await store.GetAsync(cancellationToken).ConfigureAwait(false) is not { } refreshToken)
		{
			return null;
		}

		using var response = await PostAsync("connect/token", new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken,
		}, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			if (await IsInvalidGrantAsync(response, cancellationToken).ConfigureAwait(false))
			{
				await store.ClearAsync(cancellationToken).ConfigureAwait(false);
				SessionEnded?.Invoke(this, EventArgs.Empty);
				return null;
			}

			response.EnsureSuccessStatusCode();
		}

		return await AcceptAsync(response, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Keeps the access token in memory and the next refresh token in the store.</summary>
	/// <returns>The new access token.</returns>
	private async Task<string> AcceptAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var tokens = TokenResponse.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
		if (tokens.RefreshToken is not null)
		{
			// The API has already rotated the refresh token, so when the store fails here (for example SecureStorage) the old
			// token is dead and the new one is lost: the owner must sign in again. Accepted, as the failure is rare.
			await store.SetAsync(tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
		}

		_grant = new AccessGrant(tokens.AccessToken, clock.GetUtcNow() + (tokens.Lifetime > RefreshMargin * 2 ? tokens.Lifetime - RefreshMargin : tokens.Lifetime / 2));
		return tokens.AccessToken;
	}

	/// <summary>Whether a refused token request says the refresh token is dead, as opposed to a server fault.</summary>
	private static async Task<bool> IsInvalidGrantAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.StatusCode != System.Net.HttpStatusCode.BadRequest)
		{
			return false;
		}

		try
		{
			using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
			return body.RootElement.ValueKind == JsonValueKind.Object
				&& body.RootElement.TryGetProperty("error", out var error)
				&& string.Equals(error.GetString(), InvalidGrant, StringComparison.Ordinal);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	/// <summary>Asks the API to revoke a refresh token.</summary>
	private async Task<bool> TryRevokeAsync(string refreshToken, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await PostAsync("connect/revoke", new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["token"] = refreshToken,
				["token_type_hint"] = "refresh_token",
			}, cancellationToken).ConfigureAwait(false);
			return response.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
	}

	/// <summary>Posts a form to one of the token service's endpoints, adding the client id.</summary>
	private async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields, CancellationToken cancellationToken)
	{
		fields["client_id"] = options.ClientId;
		using var http = httpClients.CreateClient(HttpClientName);
		using var form = new FormUrlEncodedContent(fields);
		return await http.PostAsync(new Uri(path, UriKind.Relative), form, cancellationToken).ConfigureAwait(false);
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : An access token and the moment it is replaced, swapped as one object so no reader sees half an update.
	/// </summary>
	/// <param name="Token">The access token.</param>
	/// <param name="RefreshAt">The moment the access token is replaced.</param>
	private sealed record AccessGrant(string Token, DateTimeOffset RefreshAt);
	#endregion
}
