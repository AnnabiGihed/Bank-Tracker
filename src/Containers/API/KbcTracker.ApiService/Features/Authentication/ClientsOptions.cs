namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Groups the settings of the registered clients; only the web client has any (the mobile client is public).
/// </summary>
public sealed class ClientsOptions
{
	#region Properties
	/// <summary>Gets the web app's client settings.</summary>
	public WebClientOptions Web { get; } = new();
	#endregion
}
