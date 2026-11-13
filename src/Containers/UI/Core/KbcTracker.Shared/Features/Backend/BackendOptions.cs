namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries the settings both UI hosts need to reach the backend: where it listens and, outside
///              Development, which owner identity to present to it.
/// </summary>
/// <param name="BaseUrl">The backend's base address: absolute HTTP or HTTPS, without user info, ending in a slash.</param>
/// <param name="OwnerLogin">The owner identity sent as <see cref="OwnerHeader"/>, or <see langword="null"/> or blank for none.</param>
public sealed record BackendOptions(string? BaseUrl, string? OwnerLogin = null)
{
	#region Constants
	/// <summary>The request header that carries the owner identity, as Tailscale Serve sets it.</summary>
	public const string OwnerHeader = "Tailscale-User-Login";
	#endregion
}
