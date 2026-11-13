namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The port through which a host takes the owner to the bank's consent page. The web app navigates the
///              browser there; the Android app cannot yet and says where to reconnect instead.
/// </summary>
public interface IBankConnectionLauncher
{
	#region Properties
	/// <summary>Gets a value indicating whether this host can open the bank's consent page.</summary>
	bool CanLaunch { get; }

	/// <summary>Gets what to tell the owner when <see cref="CanLaunch"/> is <see langword="false"/>, or <see langword="null"/> when it is not.</summary>
	string? UnavailableMessage { get; }
	#endregion

	#region Methods
	/// <summary>Opens the bank's consent page.</summary>
	/// <param name="authorizationUrl">The address the backend returned for the consent.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the page has been opened.</returns>
	Task LaunchAsync(Uri authorizationUrl, CancellationToken cancellationToken);
	#endregion
}
