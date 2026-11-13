namespace KbcTracker.Mobile.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Android app's <see cref="IBankConnectionLauncher"/>: it cannot open the bank's consent page, because
///              the bank returns to the web app's callback on the PC, so it tells the owner where to reconnect.
/// </summary>
public sealed class MobileBankConnectionLauncher : IBankConnectionLauncher
{
	#region IBankConnectionLauncher Implementation
	/// <inheritdoc />
	public bool CanLaunch => false;

	/// <inheritdoc />
	public string? UnavailableMessage => "Connect or reconnect the bank from the web app on your PC.";

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">Always: this host cannot launch, so callers must check <see cref="CanLaunch"/> first.</exception>
	public Task LaunchAsync(Uri authorizationUrl, CancellationToken cancellationToken)
	{
		throw new InvalidOperationException("The Android app cannot open the bank's consent page; check CanLaunch first and connect from the web app on the PC.");
	}
	#endregion
}
