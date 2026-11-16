using System.Net.Http.Headers;

namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Puts the owner's access token on every backend request (see <c>WithAccessTokenHandler</c>). The token
///              service refreshes it before it expires, so this handler never reacts to a 401: it asks once per request and
///              sends what it has. Without a session it sends no token, and the API answers 401, which the sign-in screen
///              (shown while there is no session) makes unreachable in practice.
/// </summary>
/// <param name="tokens">The token logic of the app.</param>
public sealed class AccessTokenHandler(PublicClientTokenService tokens) : DelegatingHandler
{
	#region Overrides
	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false) is { } token)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
	#endregion
}
