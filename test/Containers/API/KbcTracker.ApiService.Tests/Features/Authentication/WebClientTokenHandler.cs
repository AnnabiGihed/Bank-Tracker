using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Gives test requests a real access token: it asks the API's own token endpoint for the <c>web</c> client's
///              client-credentials token on first use and sends it as the bearer token, as the web app does.
/// </summary>
/// <param name="api">The API host that issues and checks the token.</param>
internal sealed class WebClientTokenHandler(WebApplicationFactory<Program> api) : DelegatingHandler
{
	#region Fields
	/// <summary>The token, fetched once per handler.</summary>
	private string? _token;
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		_token ??= await FetchAsync(api, cancellationToken).ConfigureAwait(false);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
	#endregion

	#region Public Methods
	/// <summary>Requests the <c>web</c> client's client-credentials token.</summary>
	/// <param name="api">The API host.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <param name="extra">Extra form fields, for a test that needs an unusual token.</param>
	/// <returns>The access token.</returns>
	public static async Task<string> FetchAsync(WebApplicationFactory<Program> api, CancellationToken cancellationToken,
		params (string Key, string Value)[] extra)
	{
		ArgumentNullException.ThrowIfNull(api);
		using var http = api.CreateClient();
		var form = new List<KeyValuePair<string, string>>
		{
			new("grant_type", "client_credentials"),
			new("client_id", "web"),
			new("client_secret", ApiFixture.WebSecret),
		};
		form.AddRange(extra.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));
		using var content = new FormUrlEncodedContent(form);
		using var response = await http.PostAsync(new Uri("/connect/token", UriKind.Relative), content, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
		return body.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("The token response has no access token.");
	}
	#endregion
}
