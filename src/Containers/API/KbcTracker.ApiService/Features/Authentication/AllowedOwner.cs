namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One account at an external provider that may use the backend: the provider's issuer and the account's
///              subject. An email address is never part of the match, because providers let it change.
/// </summary>
public sealed class AllowedOwner
{
	#region Properties
	/// <summary>Gets or sets the provider's issuer, for example <c>https://accounts.google.com</c>.</summary>
	public string Issuer { get; set; } = string.Empty;

	/// <summary>Gets or sets the account's stable subject at that provider.</summary>
	public string Subject { get; set; } = string.Empty;
	#endregion
}
