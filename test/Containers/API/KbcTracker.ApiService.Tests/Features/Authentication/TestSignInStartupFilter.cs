using System.Security.Claims;
using KbcTracker.ApiService.Features.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stands in for the external provider in tests only: <c>/test/sign-in?subject=..&amp;owner=true</c> sets the
///              short-lived sign-in cookie the provider callback would set (with the provider's issuer and subject, <c>providerSubject</c> overriding the default), so the real authorize and token endpoints
///              issue real tokens for any subject, including one that is not the owner. The product has no such path.
/// </summary>
public sealed class TestSignInStartupFilter : IStartupFilter
{
	#region Constants
	/// <summary>The test-only address.</summary>
	public const string Path = "/test/sign-in";

	/// <summary>The issuer of the stand-in provider; <see cref="ApiFixture"/> allow-lists it with <see cref="ProviderSubject"/>.</summary>
	public const string Issuer = "https://test-provider.invalid";

	/// <summary>The subject at the stand-in provider that the owner's sign-in carries unless a test passes another.</summary>
	public const string ProviderSubject = "provider-owner";
	#endregion

	#region IStartupFilter Implementation
	/// <inheritdoc />
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
	{
		app.Use(async (context, continuation) =>
		{
			if (!string.Equals(context.Request.Path.Value, Path, StringComparison.Ordinal))
			{
				await continuation(context).ConfigureAwait(false);
				return;
			}

			var identity = new ClaimsIdentity("test", "name", "role");
			identity.AddClaim(new Claim("sub", context.Request.Query["subject"].ToString()));
			if (string.Equals(context.Request.Query["owner"], "true", StringComparison.Ordinal))
			{
				identity.AddClaim(new Claim(OwnerIdentity.ClaimType, OwnerIdentity.Id));
				identity.AddClaim(new Claim(OwnerIdentity.ProviderIssuerClaim, Issuer));
				identity.AddClaim(new Claim(OwnerIdentity.ProviderSubjectClaim, context.Request.Query["providerSubject"].FirstOrDefault() ?? ProviderSubject));
			}

			await context.SignInAsync(OwnerIdentity.SignInCookie, new ClaimsPrincipal(identity)).ConfigureAwait(false);
			context.Response.StatusCode = StatusCodes.Status204NoContent;
		});
		next(app);
	};
	#endregion
}
