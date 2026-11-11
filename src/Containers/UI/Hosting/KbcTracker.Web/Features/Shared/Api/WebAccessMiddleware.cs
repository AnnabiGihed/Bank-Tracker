using System.Net;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Applies the trusted-loopback owner boundary before the web host can call the backend.
/// </summary>
/// <param name="next">The protected web pipeline.</param>
/// <param name="environment">The hosting environment.</param>
/// <param name="configuration">The configured owner identity.</param>
public sealed class WebAccessMiddleware(RequestDelegate next, IWebHostEnvironment environment, IConfiguration configuration)
{
	#region Constants
	/// <summary>The identity supplied by Tailscale Serve.</summary>
	public const string IdentityHeader = "Tailscale-User-Login";
	#endregion

	#region Public Methods
	/// <summary>Rejects untrusted peers, missing owners and cross-origin browser writes.</summary>
	/// <param name="context">The request to authorize.</param>
	/// <returns>A task that completes after authorization and dispatch.</returns>
	/// <exception cref="ArgumentNullException">The request is null.</exception>
	public async Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		context.Response.Headers.CacheControl = "no-store";
		context.Response.Headers["Referrer-Policy"] = "no-referrer";
		var remote = context.Connection.RemoteIpAddress;
		if (remote?.IsIPv4MappedToIPv6 is true)
		{
			remote = remote.MapToIPv4();
		}

		var local = remote is null ? environment.IsDevelopment() : IPAddress.IsLoopback(remote);
		var owner = configuration["Backend:OwnerLogin"];
		var authorized = local && (environment.IsDevelopment() || (!string.IsNullOrWhiteSpace(owner)
			&& string.Equals(context.Request.Headers[IdentityHeader].ToString(), owner, StringComparison.Ordinal)));
		if (!authorized || IsCrossOriginWrite(context.Request))
		{
			await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Web access denied.").ExecuteAsync(context).ConfigureAwait(false);
			return;
		}

		await next(context).ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>Protects mutations while allowing the state-validated bank callback navigation.</summary>
	private static bool IsCrossOriginWrite(HttpRequest request)
	{
		if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
		{
			return false;
		}

		var origin = request.Headers.Origin.ToString();
		return string.Equals(request.Headers["Sec-Fetch-Site"].ToString(), "cross-site", StringComparison.OrdinalIgnoreCase)
			|| (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var address)
				|| !string.Equals(address.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)));
	}
	#endregion
}
