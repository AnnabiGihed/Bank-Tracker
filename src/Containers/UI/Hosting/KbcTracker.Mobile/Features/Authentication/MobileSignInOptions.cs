namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The sign-in settings of the app: which providers the sign-in screen offers, from the embedded
///              <c>SignIn:Providers</c> setting (the API has no provider list to ask for).
/// </summary>
/// <param name="Providers">The provider names, as the API knows them (for example <c>Google</c>).</param>
public sealed record MobileSignInOptions(IReadOnlyList<string> Providers);
