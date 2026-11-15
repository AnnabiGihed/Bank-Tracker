using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Receives the owner back from the external provider (<c>/callback/login/google</c>). The provider's issuer
///              and the account's subject must be on the allow-list; only then does the short-lived sign-in cookie, which
///              the authorize endpoint reads, get the one owner id. Anyone else is refused with a clear message.
/// </summary>
/// <param name="allowList">The configured owner accounts.</param>
/// <param name="client">The OpenIddict client, which knows each registration's issuer.</param>
/// <param name="logger">Tells the owner which issuer and subject a refused account has.</param>
[ApiController]
[Route("callback/login")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ProviderCallbackController(OwnerAllowList allowList, OpenIddictClientService client, ILogger<ProviderCallbackController> logger) : ControllerBase
{
	#region Endpoints
	/// <summary>Completes the provider sign-in.</summary>
	/// <returns>A redirect back to the authorize request, or a problem for a failed or unauthorised sign-in.</returns>
	[AllowAnonymous]
	[HttpGet("{provider}")]
	[HttpPost("{provider}")]
	public async Task<IActionResult> LogIn()
	{
		var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
		if (result.Principal is not { Identity.IsAuthenticated: true } external)
		{
			return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Sign-in failed.",
				detail: "The provider did not confirm your account. Try signing in again.");
		}

		var registration = await client.GetClientRegistrationByIdAsync(external.FindFirstValue(Claims.Private.RegistrationId) ?? string.Empty, HttpContext.RequestAborted)
			.ConfigureAwait(false);
		var subject = external.FindFirstValue(ClaimTypes.NameIdentifier) ?? external.FindFirstValue(Claims.Subject);
		if (allowList.Map(registration.Issuer?.AbsoluteUri, subject) is not { } owner)
		{
			logger.LogAccountRefused(registration.Issuer?.AbsoluteUri, subject);
			return Problem(statusCode: StatusCodes.Status403Forbidden, title: "This account may not use KbcTracker.",
				detail: "The account you signed in with is not the owner of this backend. Sign in with the owner's account.");
		}

		var identity = new ClaimsIdentity(OwnerIdentity.SignInCookie, Claims.Name, Claims.Role);
		identity.SetClaim(Claims.Subject, owner);
		identity.SetClaim(OwnerIdentity.ClaimType, owner);
		identity.SetClaim(OwnerIdentity.ProviderIssuerClaim, registration.Issuer?.AbsoluteUri);
		identity.SetClaim(OwnerIdentity.ProviderSubjectClaim, subject);
		await HttpContext.SignInAsync(OwnerIdentity.SignInCookie, new ClaimsPrincipal(identity)).ConfigureAwait(false);
		return LocalRedirect(result.Properties?.RedirectUri ?? "/");
	}
	#endregion
}
