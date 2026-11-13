namespace KbcTracker.Mobile.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The MAUI application: it opens one window that shows the <see cref="MainPage"/>. Its base type is
///              written in full because, inside the KbcTracker namespace, "Application" means the
///              KbcTracker.Application project.
/// </summary>
public sealed class App : Microsoft.Maui.Controls.Application
{
	#region Protected Methods
	/// <inheritdoc />
	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "KbcTracker" };
	}
	#endregion
}
