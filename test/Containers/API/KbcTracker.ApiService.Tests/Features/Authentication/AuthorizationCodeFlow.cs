using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs the real authorization code flow with PKCE against a test host, the way a client does: the sign-in
///              cookie is set through <see cref="TestSignInStartupFilter"/>, then the authorize and token endpoints
///              answer. It returns the tokens, or the raw token response when the exchange fails.
/// </summary>
internal sealed class AuthorizationCodeFlow
{
	#region Fields
	/// <summary>The API host under test.</summary>
	private readonly WebApplicationFactory<Program> _api;

	/// <summary>The client that keeps the sign-in cookie between the calls; it follows no redirect.</summary>
	private readonly HttpClient _browser;
	#endregion

	#region Constructors
	/// <summary>Starts a flow against a host.</summary>
	/// <param name="api">The API host.</param>
	public AuthorizationCodeFlow(WebApplicationFactory<Program> api)
	{
		_api = api;
		_browser = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
	}
	#endregion

	#region Public Methods
	/// <summary>Signs a subject in at the stand-in provider.</summary>
	/// <param name="subject">The subject the sign-in cookie carries.</param>
	/// <param name="owner">Whether the cookie carries the owner claim, as the real callback sets only for the owner.</param>
	/// <param name="providerSubject">The subject at the provider the cookie carries, or <see langword="null"/> for the allow-listed one.</param>
	/// <returns>A task that completes when the cookie is set.</returns>
	public async Task SignInAsync(string subject, bool owner, string? providerSubject = null)
	{
		var provider = providerSubject is null ? string.Empty : $"&providerSubject={Uri.EscapeDataString(providerSubject)}";
		using var response = await _browser.GetAsync(new Uri($"{TestSignInStartupFilter.Path}?subject={Uri.EscapeDataString(subject)}&owner={(owner ? "true" : "false")}{provider}", UriKind.Relative))
			.ConfigureAwait(false);
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	/// <summary>Asks the authorize endpoint for a code and exchanges it for tokens; a refusal there is returned as it is.</summary>
	/// <param name="clientId">The registered client.</param>
	/// <param name="secret">The client's secret, or <see langword="null"/> for a public client.</param>
	/// <param name="redirectUri">The redirect address the client registered.</param>
	/// <param name="usePkce">Whether the request carries a code challenge; the server must refuse one that does not.</param>
	/// <returns>The status and body of the token response, or of the authorize endpoint's refusal.</returns>
	public async Task<(HttpStatusCode Status, JsonElement Body)> ExchangeAsync(string clientId, string? secret, string redirectUri, bool usePkce = true)
	{
		var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
		var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
		var query = new Dictionary<string, string?>
		{
			["client_id"] = clientId,
			["redirect_uri"] = redirectUri,
			["response_type"] = "code",
			["scope"] = "openid offline_access",
			["state"] = "state-value",
		};
		if (usePkce)
		{
			query["code_challenge"] = challenge;
			query["code_challenge_method"] = "S256";
		}

		using var authorize = await _browser.GetAsync(new Uri(QueryHelpers.AddQueryString("/connect/authorize", query), UriKind.Relative)).ConfigureAwait(false);
		if (authorize.StatusCode != HttpStatusCode.Redirect)
		{
			// The endpoint answers a request it cannot redirect with text such as "error:invalid_request".
			var refusal = await authorize.Content.ReadAsStringAsync().ConfigureAwait(false);
			var error = refusal.Split('\n').FirstOrDefault(line => line.StartsWith("error:", StringComparison.Ordinal))?["error:".Length..].Trim() ?? refusal;
			return (authorize.StatusCode, JsonSerializer.SerializeToElement(new { error }));
		}

		var code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"];

		var form = new Dictionary<string, string>
		{
			["grant_type"] = "authorization_code",
			["code"] = code.ToString(),
			["redirect_uri"] = redirectUri,
			["client_id"] = clientId,
			["code_verifier"] = verifier,
		};
		if (secret is not null)
		{
			form["client_secret"] = secret;
		}

		return await PostTokenAsync(form).ConfigureAwait(false);
	}

	/// <summary>Redeems a refresh token.</summary>
	/// <param name="clientId">The client the token was issued to.</param>
	/// <param name="secret">The client's secret, or <see langword="null"/> for a public client.</param>
	/// <param name="refreshToken">The refresh token.</param>
	/// <returns>The status and body of the token response.</returns>
	public async Task<(HttpStatusCode Status, JsonElement Body)> RefreshAsync(string clientId, string? secret, string refreshToken)
	{
		var form = new Dictionary<string, string>
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken,
			["client_id"] = clientId,
		};
		if (secret is not null)
		{
			form["client_secret"] = secret;
		}

		return await PostTokenAsync(form).ConfigureAwait(false);
	}

	/// <summary>Sends a request to the API with the access token.</summary>
	/// <param name="accessToken">The bearer token.</param>
	/// <param name="path">The API path.</param>
	/// <returns>The response status.</returns>
	public async Task<HttpStatusCode> GetAsync(string accessToken, string path)
	{
		using var client = _api.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
		request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
		using var response = await client.SendAsync(request).ConfigureAwait(false);
		return response.StatusCode;
	}
	#endregion

	#region Private Helpers
	/// <summary>Posts a form to the token endpoint.</summary>
	private async Task<(HttpStatusCode, JsonElement)> PostTokenAsync(Dictionary<string, string> form)
	{
		using var content = new FormUrlEncodedContent(form);
		using var response = await _browser.PostAsync(new Uri("/connect/token", UriKind.Relative), content).ConfigureAwait(false);
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
		return (response.StatusCode, body.RootElement.Clone());
	}
	#endregion
}
