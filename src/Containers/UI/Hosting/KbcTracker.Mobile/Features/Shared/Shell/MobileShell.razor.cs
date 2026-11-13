namespace KbcTracker.Mobile.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The root component of the Android app: it renders the shared <see cref="KbcTrackerRouter"/>. Unlike the
///              web app's shell it passes no one-time notice, because the bank's callback never reaches this app.
/// </summary>
public sealed partial class MobileShell : ComponentBase
{
}
