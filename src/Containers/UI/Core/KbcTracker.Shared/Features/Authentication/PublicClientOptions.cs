namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies a public OAuth client (the Android app) to the API's token service: where the service is, the
///              registered client id, and the redirect address the browser returns to.
/// </summary>
/// <param name="Authority">The API's base address, ending in a slash; the endpoints are below <c>connect/</c>.</param>
/// <param name="ClientId">The client id the API registered.</param>
/// <param name="RedirectUri">The redirect address the API registered for the client.</param>
public sealed record PublicClientOptions(Uri Authority, string ClientId, Uri RedirectUri);
