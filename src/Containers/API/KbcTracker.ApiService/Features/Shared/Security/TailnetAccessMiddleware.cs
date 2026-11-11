using System.Net;
using Microsoft.Extensions.Options;

namespace KbcTracker.ApiService.Features.Shared.Security;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Rejects non-loopback data access and requires the configured Tailscale identity outside development.
/// </summary>
/// <param name="next">The protected pipeline.</param>
/// <param name="environment">The host environment.</param>
/// <param name="options">The owner identity, supplied outside source control.</param>
public sealed class TailnetAccessMiddleware(RequestDelegate next, IWebHostEnvironment environment, IOptions<BackendAccessOptions> options)
{
	#region Constants
	/// <summary>The identity header supplied by a trusted Tailscale Serve proxy.</summary>
	public const string IdentityHeader = "Tailscale-User-Login";

	/// <summary>The browser-supplied site relationship used to reject cross-site mutations.</summary>
	private const string FetchSiteHeader = "Sec-Fetch-Site";
	#endregion

	#region Public Methods
	/// <summary>Protects every data and documentation endpoint before dispatch.</summary>
	/// <param name="context">The current request.</param>
	/// <returns>A task that completes after the response.</returns>
	/// <exception cref="ArgumentNullException">The request context is null.</exception>
	public async Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (string.Equals(context.Request.Path.Value, ApiRoutes.Health, StringComparison.Ordinal))
		{
			await next(context).ConfigureAwait(false);
			return;
		}

		var remote = context.Connection.RemoteIpAddress;
		if (remote?.IsIPv4MappedToIPv6 is true)
		{
			remote = remote.MapToIPv4();
		}

		var local = remote is null ? environment.IsDevelopment() : IPAddress.IsLoopback(remote);
		var owner = options.Value.OwnerLogin;
		var trusted = local && (environment.IsDevelopment() || (!string.IsNullOrWhiteSpace(owner)
			&& string.Equals(context.Request.Headers[IdentityHeader].ToString(), owner, StringComparison.Ordinal)));
		if (!trusted || IsCrossOriginWrite(context.Request))
		{
			await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Backend access denied.")
				.ExecuteAsync(context).ConfigureAwait(false);
			return;
		}

		await next(context).ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>Prevents a browser on another site from triggering a mutation through the trusted proxy.</summary>
	private static bool IsCrossOriginWrite(HttpRequest request)
	{
		if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
		{
			return false;
		}

		if (string.Equals(request.Headers[FetchSiteHeader].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var origin = request.Headers.Origin.ToString();
		return !string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var address)
			|| !string.Equals(address.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase));
	}
	#endregion
}
