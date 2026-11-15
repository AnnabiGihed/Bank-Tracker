namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Names the fixed identifiers of the web app's sign-in (ADR-0009) in one place: the cookie session, the
///              claims it holds, the sign-in and sign-out addresses and the settings it reads. The claim name and the
///              client id are the contract with the API's token service.
/// </summary>
public static class WebAuthentication
{
	#region Constants
	/// <summary>The name of the cookie authentication scheme that holds the owner's session.</summary>
	public const string CookieScheme = "kbc-web";

	/// <summary>The name of the session cookie.</summary>
	public const string CookieName = "kbc-session";

	/// <summary>The claim in the API's identity token that holds the owner id; a session without it is not the owner's.</summary>
	public const string OwnerClaim = "kbc_owner";

	/// <summary>The value of <see cref="OwnerClaim"/> that identifies the owner.</summary>
	public const string OwnerId = "owner";

	/// <summary>The claim that holds when the session ends (Unix seconds), so a live circuit cannot outlive the cookie.</summary>
	public const string SessionEndClaim = "kbc_session_end";

	/// <summary>The name under which the refresh token is kept in the session, for revocation at sign-out.</summary>
	public const string RefreshTokenName = "refresh_token";

	/// <summary>The client id of the web app at the API.</summary>
	public const string ClientId = "web";

	/// <summary>The address of the sign-in page.</summary>
	public const string SignInPath = "/signin";

	/// <summary>The address that sends the browser to the API's sign-in with one provider.</summary>
	public const string SignInStartPath = "/signin/start";

	/// <summary>The address that ends the session.</summary>
	public const string SignOutPath = "/signout";

	/// <summary>The query parameter that carries the page to return to after signing in.</summary>
	public const string ReturnUrlParameter = "returnUrl";

	/// <summary>The setting that holds the client secret of the web app, shared with the API.</summary>
	public const string SecretSetting = "Authentication:Clients:Web:Secret";

	/// <summary>The optional setting that holds the address the browser reaches the API at; <c>Backend:BaseUrl</c> is used when it is absent.</summary>
	public const string AuthoritySetting = "Authentication:Authority";

	/// <summary>The setting that lists the providers the sign-in page offers.</summary>
	public const string ProvidersSetting = "SignIn:Providers";
	#endregion

	#region Static Instances
	/// <summary>How long a session lasts before the owner signs in again.</summary>
	public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
	#endregion
}
