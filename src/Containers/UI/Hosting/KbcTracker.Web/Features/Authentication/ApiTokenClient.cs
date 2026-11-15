using System.Text.Json;

namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Talks to the API's token endpoints as the <c>web</c> client: obtains the client-credentials access token
///              the backend calls carry, caches it and fetches a new one shortly before it expires, and revokes an owner's
///              refresh token at sign-out. One instance serves the whole host, so the cache is shared.
/// </summary>
/// <param name="httpClients">Creates the HTTP client that reaches the API.</param>
/// <param name="secret">The client secret of the web app.</param>
/// <param name="clock">The clock the expiry is measured with.</param>
public sealed class ApiTokenClient(IHttpClientFactory httpClients, WebClientSecret secret, TimeProvider clock) : IDisposable
{
	#region Constants
	/// <summary>The name of the HTTP client that reaches the API's token endpoints.</summary>
	public const string HttpClientName = "kbc-api-token";
	#endregion

	#region Static Instances
	/// <summary>How long before its expiry a cached token is replaced.</summary>
	private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
	#endregion

	#region Fields
	/// <summary>Lets one request at a time fetch a token while the others wait for it.</summary>
	private readonly SemaphoreSlim _gate = new(1, 1);

	/// <summary>The cached token with its refresh time, swapped as one object so a reader never sees a mix of two tokens; <see langword="null"/> before the first one.</summary>
	private volatile CachedToken? _cached;
	#endregion

	#region Public Methods
	/// <summary>Gets the access token of the web client, fetching a new one when the cached one is about to expire.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The access token.</returns>
	/// <exception cref="HttpRequestException">The API refused or did not answer the token request.</exception>
	/// <exception cref="InvalidOperationException">The token response is not valid.</exception>
	public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		if (_cached is { } current && IsFresh(current))
		{
			return current.Value;
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_cached is { } latest && IsFresh(latest))
			{
				return latest.Value;
			}

			return await FetchAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Revokes the owner's refresh token at the API, so a copy of it is worthless after sign-out.</summary>
	/// <param name="refreshToken">The refresh token kept in the session.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>A task that completes when the API answered.</returns>
	/// <exception cref="HttpRequestException">The API refused or did not answer the request.</exception>
	public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(refreshToken);
		using var http = httpClients.CreateClient(HttpClientName);
		using var form = WithClient(new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["token"] = refreshToken,
			["token_type_hint"] = "refresh_token",
		});
		using var response = await http.PostAsync(new Uri("connect/revoke", UriKind.Relative), form, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => _gate.Dispose();
	#endregion

	#region Private Helpers
	/// <summary>Whether the cached token can still be used.</summary>
	private bool IsFresh(CachedToken cached) => clock.GetUtcNow() < cached.RefreshAt;

	/// <summary>Requests a client-credentials token, caches it and returns it.</summary>
	private async Task<string> FetchAsync(CancellationToken cancellationToken)
	{
		using var http = httpClients.CreateClient(HttpClientName);
		using var form = WithClient(new Dictionary<string, string>(StringComparer.Ordinal) { ["grant_type"] = "client_credentials" });
		using var response = await http.PostAsync(new Uri("connect/token", UriKind.Relative), form, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
		var token = body.RootElement.TryGetProperty("access_token", out var value) ? value.GetString() : null;
		if (string.IsNullOrEmpty(token) || !body.RootElement.TryGetProperty("expires_in", out var seconds) || !seconds.TryGetInt32(out var lifetime) || lifetime <= 0)
		{
			throw new InvalidOperationException("The token response has no access token or lifetime.");
		}

		var life = TimeSpan.FromSeconds(lifetime);
		_cached = new CachedToken(token, clock.GetUtcNow() + (life > RefreshMargin * 2 ? life - RefreshMargin : life / 2));
		return token;
	}

	/// <summary>Adds the web client's id and secret to a token or revocation request.</summary>
	private FormUrlEncodedContent WithClient(Dictionary<string, string> fields)
	{
		fields["client_id"] = WebAuthentication.ClientId;
		fields["client_secret"] = secret.Value;
		return new FormUrlEncodedContent(fields);
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : An access token and the moment it is replaced, swapped as one object so no reader sees half an update.
	/// </summary>
	/// <param name="Value">The access token.</param>
	/// <param name="RefreshAt">The moment the token is replaced.</param>
	private sealed record CachedToken(string Value, DateTimeOffset RefreshAt);
	#endregion
}
