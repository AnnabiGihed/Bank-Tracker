using System.Net.Http.Headers;

namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Puts the web client's access token on every backend request (see <c>WithAccessTokenHandler</c>). The
///              token comes from the shared <see cref="ApiTokenClient"/>, which caches it and refreshes it before it expires.
///              The API accepts it as the owner's because the owner has signed in to this web app first.
/// </summary>
/// <param name="tokens">The token source shared by the host.</param>
public sealed class ApiAccessTokenHandler(ApiTokenClient tokens) : DelegatingHandler
{
	#region Overrides
	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false));
		return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
	#endregion
}
