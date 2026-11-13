namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Opens the bank's consent page by leaving the app for the address the backend returned; the bank sends
///              the browser back to <c>/callback</c> on this host.
/// </summary>
/// <param name="navigation">The navigation manager of the circuit.</param>
public sealed class WebBankConnectionLauncher(NavigationManager navigation) : IBankConnectionLauncher
{
	#region IBankConnectionLauncher Implementation
	/// <inheritdoc />
	public bool CanLaunch => true;

	/// <inheritdoc />
	public string? UnavailableMessage => null;

	/// <inheritdoc />
	public Task LaunchAsync(Uri authorizationUrl, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(authorizationUrl);
		navigation.NavigateTo(authorizationUrl.ToString(), forceLoad: true);
		return Task.CompletedTask;
	}
	#endregion
}
