namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The browser-facing protections every response and write of the web host gets: no caching, no referrer, and
///              the refusal of a write or WebSocket upgrade that another site started. Who may reach the app is decided by
///              the owner's sign-in (the fallback authorization policy), not here.
/// </summary>
/// <param name="next">The protected web pipeline.</param>
public sealed class WebAccessMiddleware(RequestDelegate next)
{
	#region Public Methods
	/// <summary>Adds the response headers and rejects cross-origin browser writes.</summary>
	/// <param name="context">The request to check.</param>
	/// <returns>A task that completes after the check and dispatch.</returns>
	/// <exception cref="ArgumentNullException">The request is null.</exception>
	public async Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Response.Headers.CacheControl = "no-store";
		context.Response.Headers["Referrer-Policy"] = "no-referrer";
		if (IsCrossOriginWrite(context.Request))
		{
			await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Web access denied.").ExecuteAsync(context).ConfigureAwait(false);
			return;
		}

		await next(context).ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>Protects mutations while allowing the state-validated bank callback navigation. A browser's
	/// <c>Sec-Fetch-Site</c> decides when present: only this site's own pages (<c>same-origin</c>) or the owner typing
	/// the address (<c>none</c>) may write. <c>Origin</c> is not enough on its own: the pages send
	/// <c>Referrer-Policy: no-referrer</c>, under which browsers can send <c>Origin: null</c> for the app's own requests.
	/// The only writes today are the Blazor circuit's WebSocket and its negotiate POST; the bank callback is a GET.
	/// Clients without <c>Sec-Fetch-Site</c> fall back to the <c>Origin</c> check. A WebSocket upgrade counts as a
	/// write although it is a GET: every change the pages make travels over the Blazor circuit's socket, and a page on
	/// another site could otherwise open one.</summary>
	private static bool IsCrossOriginWrite(HttpRequest request)
	{
		var safeMethod = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
		if (safeMethod && !IsWebSocketUpgrade(request))
		{
			return false;
		}

		var site = request.Headers["Sec-Fetch-Site"].ToString();
		if (!string.IsNullOrEmpty(site))
		{
			return !(string.Equals(site, "same-origin", StringComparison.OrdinalIgnoreCase) || string.Equals(site, "none", StringComparison.OrdinalIgnoreCase));
		}

		var origin = request.Headers.Origin.ToString();
		return !string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var address)
			|| !string.Equals(address.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Recognises a WebSocket handshake by its headers, which every server sees, rather than by the server's
	/// WebSocket feature, which a test host may not offer.</summary>
	/// <param name="request">The incoming request.</param>
	/// <returns><see langword="true"/> when the request asks to upgrade to a WebSocket.</returns>
	private static bool IsWebSocketUpgrade(HttpRequest request) =>
		string.Equals(request.Headers.Upgrade.ToString(), "websocket", StringComparison.OrdinalIgnoreCase);
	#endregion
}
