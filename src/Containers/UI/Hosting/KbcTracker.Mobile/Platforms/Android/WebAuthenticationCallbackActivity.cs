using Android.App;
using Android.Content;
using Android.Content.PM;

namespace KbcTracker.Mobile;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Catches the browser's return to the app after sign-in: the API redirects to
///              <c>com.banktracker.mobile://auth</c>, this activity's intent filter claims that address, and MAUI hands the
///              result to the pending <see cref="WebAuthenticator"/> call. It has no screen of its own.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
	DataScheme = CallbackScheme, DataHost = CallbackHost)]
public sealed class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
	#region Constants
	/// <summary>The scheme of the redirect address the API registered for the app; Android requires it in lower case.</summary>
	private const string CallbackScheme = "com.banktracker.mobile";

	/// <summary>The host of the redirect address.</summary>
	private const string CallbackHost = "auth";
	#endregion
}
