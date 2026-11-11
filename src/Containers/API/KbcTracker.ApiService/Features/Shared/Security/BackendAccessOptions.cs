namespace KbcTracker.ApiService.Features.Shared.Security;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies the sole tailnet owner allowed to use the backend outside local development.
/// </summary>
public sealed class BackendAccessOptions
{
	#region Constants
	/// <summary>The configuration section for the trusted-tailnet boundary.</summary>
	public const string SectionName = "BackendAccess";
	#endregion

	#region Properties
	/// <summary>Gets or sets the exact Tailscale owner login; blank denies all non-development data requests.</summary>
	public string OwnerLogin { get; set; } = string.Empty;
	#endregion
}
