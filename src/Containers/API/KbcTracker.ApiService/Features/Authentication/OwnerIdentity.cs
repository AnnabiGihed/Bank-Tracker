namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Names the fixed identifiers of the owner's sign-in (ADR-0009) in one place: the one owner id, the claim
///              that carries it, the "Owner" policy, the token audience and the two registered clients.
/// </summary>
public static class OwnerIdentity
{
	#region Constants
	/// <summary>The one stable owner id every allow-listed provider account maps to; it also binds a bank consent.</summary>
	public const string Id = "owner";

	/// <summary>The claim in an access token that holds <see cref="Id"/>, for an owner's user token and for the web client's.</summary>
	public const string ClaimType = "kbc_owner";

	/// <summary>The claim that carries the provider's issuer in the sign-in cookie, the authorization code and the refresh token (never in an access token), so a refresh can check the allow-list again.</summary>
	public const string ProviderIssuerClaim = "kbc_provider_issuer";

	/// <summary>The claim that carries the account's subject at the provider; kept like <see cref="ProviderIssuerClaim"/>.</summary>
	public const string ProviderSubjectClaim = "kbc_provider_subject";

	/// <summary>The fallback authorization policy every endpoint but the sign-in endpoints and health is held to.</summary>
	public const string PolicyName = "Owner";

	/// <summary>The audience of every access token the API issues and the only one it accepts.</summary>
	public const string Audience = "kbctracker-api";

	/// <summary>The confidential client of the web app.</summary>
	public const string WebClientId = "web";

	/// <summary>The public client of the Android app.</summary>
	public const string MobileClientId = "mobile";

	/// <summary>The custom URI scheme the Android app registers for the browser's return to it.</summary>
	public const string MobileScheme = "com.banktracker.mobile";

	/// <summary>The host part of the Android app's sign-in redirect address.</summary>
	public const string MobileRedirectHost = "auth";

	/// <summary>The name of the short-lived cookie that keeps the owner between the provider's callback and the authorize endpoint.</summary>
	public const string SignInCookie = "kbc-signin";
	#endregion

	#region Properties
	/// <summary>Gets the redirect address of the Android app's sign-in, <c>com.banktracker.mobile://auth</c>.</summary>
	public static Uri MobileRedirectUri { get; } = new(MobileScheme + Uri.SchemeDelimiter + MobileRedirectHost);
	#endregion
}
