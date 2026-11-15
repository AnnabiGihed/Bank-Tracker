namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Where the owner is in signing in to the app.
/// </summary>
public enum SessionState
{
	/// <summary>The app is still looking for a stored session.</summary>
	Starting,

	/// <summary>Nobody is signed in: the sign-in screen is shown.</summary>
	SignedOut,

	/// <summary>The owner is signed in: the tabs are shown.</summary>
	SignedIn,
}
