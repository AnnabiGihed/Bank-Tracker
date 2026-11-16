using System.Security.Claims;
using KbcTracker.Web.Features.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.E2E.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The test-only way to be signed in to the web host without a real provider: a route in front of the product
///              pipeline that issues the product's own session cookie for a chosen account, plus a route that hands out an
///              antiforgery token for the sign-out post. It exists only in the test assembly; the product has no bypass.
/// </summary>
public sealed class TestSignInStartupFilter : IStartupFilter
{
	#region Constants
	/// <summary>The route that signs a test account in.</summary>
	public const string SignInRoute = "/__test/signin";

	/// <summary>The route that returns an antiforgery request token and sets its cookie.</summary>
	public const string AntiforgeryRoute = "/__test/antiforgery";
	#endregion

	#region IStartupFilter Implementation
	/// <inheritdoc />
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		ArgumentNullException.ThrowIfNull(next);
		return app =>
		{
			app.Use(async (context, continuation) =>
			{
				if (context.Request.Path.Equals(SignInRoute, StringComparison.Ordinal))
				{
					var owner = context.Request.Query["owner"].ToString();
					var identity = new ClaimsIdentity(
						[new Claim("sub", "test-subject"), new Claim(WebAuthentication.OwnerClaim, owner.Length > 0 ? owner : WebAuthentication.OwnerId)],
						"test");
					await context.SignInAsync(WebAuthentication.CookieScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
					await context.Response.WriteAsync("signed in").ConfigureAwait(false);
					return;
				}

				if (context.Request.Path.Equals(AntiforgeryRoute, StringComparison.Ordinal))
				{
					// The token is bound to the signed-in user, and this route runs before the product's authentication middleware.
					context.User = (await context.AuthenticateAsync(WebAuthentication.CookieScheme).ConfigureAwait(false)).Principal ?? context.User;
					var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
					await context.Response.WriteAsync(tokens.RequestToken ?? string.Empty).ConfigureAwait(false);
					return;
				}

				await continuation(context).ConfigureAwait(false);
			});
			next(app);
		};
	}
	#endregion
}
