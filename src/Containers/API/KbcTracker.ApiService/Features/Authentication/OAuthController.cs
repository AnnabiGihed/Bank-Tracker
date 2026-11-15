using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The authorize and token endpoints of the API's token service (ADR-0009). OpenIddict validates every
///              request first and hands the valid ones to these actions, which only decide who the token is for:
///              the owner the provider callback signed in, or the web client itself for its client-credentials token.
/// </summary>
/// <param name="options">The configured providers.</param>
/// <param name="allowList">The configured owner accounts, checked again when a refresh token is redeemed.</param>
/// <param name="authorizations">Revokes the authorization of an account that is no longer allowed.</param>
/// <param name="tokens">Revokes the tokens of that authorization.</param>
[ApiController]
[Route("connect")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class OAuthController(OwnerSignInOptions options, OwnerAllowList allowList, IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens) : ControllerBase
{
	#region Constants
	/// <summary>The authentication type of the principals handed to OpenIddict.</summary>
	private const string TokenAuthenticationType = "KbcTracker";

	/// <summary>The optional request parameter that names the provider to sign in with.</summary>
	private const string ProviderParameter = "provider";
	#endregion

	#region Endpoints
	/// <summary>Issues an authorization code to a client once the owner is signed in, or sends the owner to the provider.</summary>
	/// <returns>The code response, the provider challenge, or an OpenID Connect error.</returns>
	/// <exception cref="InvalidOperationException">The OpenID Connect request cannot be read.</exception>
	[AllowAnonymous]
	[HttpGet("authorize")]
	[HttpPost("authorize")]
	public async Task<IActionResult> Authorize()
	{
		var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
		var signedIn = await HttpContext.AuthenticateAsync(OwnerIdentity.SignInCookie).ConfigureAwait(false);
		if (!signedIn.Succeeded || signedIn.Principal is null)
		{
			return request.HasPromptValue(PromptValues.None)
				? ServerError(Errors.LoginRequired, "The owner is not signed in.")
				: SignInAtProvider(request);
		}

		var identity = new ClaimsIdentity(TokenAuthenticationType, Claims.Name, Claims.Role);
		identity.SetClaim(Claims.Subject, signedIn.Principal.FindFirstValue(Claims.Subject));
		if (signedIn.Principal.FindFirstValue(OwnerIdentity.ClaimType) is { } owner)
		{
			identity.SetClaim(OwnerIdentity.ClaimType, owner);
		}

		identity.SetClaim(OwnerIdentity.ProviderIssuerClaim, signedIn.Principal.FindFirstValue(OwnerIdentity.ProviderIssuerClaim));
		identity.SetClaim(OwnerIdentity.ProviderSubjectClaim, signedIn.Principal.FindFirstValue(OwnerIdentity.ProviderSubjectClaim));
		return SignIn(Describe(identity, request.GetScopes()), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
	}

	/// <summary>Exchanges a code or refresh token, or a client's credentials, for tokens.</summary>
	/// <returns>The token response, or an OpenID Connect error.</returns>
	/// <exception cref="InvalidOperationException">The OpenID Connect request cannot be read or its grant is not supported.</exception>
	[AllowAnonymous]
	[HttpPost("token")]
	[Produces("application/json")]
	public async Task<IActionResult> Exchange()
	{
		var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
		if (request.IsClientCredentialsGrantType())
		{
			var identity = new ClaimsIdentity(TokenAuthenticationType, Claims.Name, Claims.Role);
			identity.SetClaim(Claims.Subject, request.ClientId);
			if (string.Equals(request.ClientId, OwnerIdentity.WebClientId, StringComparison.Ordinal))
			{
				identity.SetClaim(OwnerIdentity.ClaimType, OwnerIdentity.Id);
			}

			return SignIn(Describe(identity, request.GetScopes()), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
		}

		if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
		{
			var redeemed = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
			if (redeemed.Principal is null)
			{
				return ServerError(Errors.InvalidGrant, "The token is no longer valid.");
			}

			// A refresh token outlives the sign-in, so the allow-list is checked again: removing an account from it ends its access.
			if (request.IsRefreshTokenGrantType() && allowList.Map(redeemed.Principal.GetClaim(OwnerIdentity.ProviderIssuerClaim), redeemed.Principal.GetClaim(OwnerIdentity.ProviderSubjectClaim)) is null)
			{
				await RevokeAuthorizationAsync(redeemed.Principal).ConfigureAwait(false);
				return ServerError(Errors.InvalidGrant, "The account is no longer allowed to use this backend.");
			}

			return SignIn(redeemed.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
		}

		throw new InvalidOperationException("The specified grant type is not supported.");
	}
	#endregion

	#region Private Helpers
	/// <summary>Gives the identity its scopes, the API as audience, and puts every claim into the access token (and the
	/// identity token, which only exists when <c>openid</c> was asked for).</summary>
	private static ClaimsPrincipal Describe(ClaimsIdentity identity, IEnumerable<string> scopes)
	{
		var principal = new ClaimsPrincipal(identity);
		principal.SetScopes(scopes);
		principal.SetResources(OwnerIdentity.Audience);

		// The provider claims stay in the authorization code and refresh token only, which keep every claim.
		principal.SetDestinations(claim => claim.Type is OwnerIdentity.ProviderIssuerClaim or OwnerIdentity.ProviderSubjectClaim
			? []
			: [Destinations.AccessToken, Destinations.IdentityToken]);
		return principal;
	}

	/// <summary>Revokes the authorization of the principal and every token issued under it.</summary>
	private async Task RevokeAuthorizationAsync(ClaimsPrincipal principal)
	{
		if (principal.GetAuthorizationId() is not { } id)
		{
			return;
		}

		if (await authorizations.FindByIdAsync(id, HttpContext.RequestAborted).ConfigureAwait(false) is { } authorization)
		{
			await authorizations.TryRevokeAsync(authorization, HttpContext.RequestAborted).ConfigureAwait(false);
		}

		await tokens.RevokeByAuthorizationIdAsync(id, HttpContext.RequestAborted).ConfigureAwait(false);
	}

	/// <summary>Sends the owner to the external provider and back to this same request once the callback has signed them in.</summary>
	private IActionResult SignInAtProvider(OpenIddictRequest request)
	{
		var requested = SignInProviders.Canonical(request.GetParameter(ProviderParameter)?.ToString())
			?? options.Providers.Keys.Select(SignInProviders.Canonical).FirstOrDefault(name => name is not null);
		if (requested is null)
		{
			return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "No sign-in provider is configured.",
				detail: "Configure Authentication:Providers:Google (or another supported provider) on the backend.");
		}

		var properties = new AuthenticationProperties(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			[OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = requested,
		})
		{
			RedirectUri = Request.PathBase + Request.Path + QueryString.Create(Request.HasFormContentType ? [.. Request.Form] : [.. Request.Query]),
		};
		return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
	}

	/// <summary>Answers the client with an OpenID Connect error.</summary>
	private ForbidResult ServerError(string error, string description) => Forbid(
		new AuthenticationProperties(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			[OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
			[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
		}),
		OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
	#endregion
}
