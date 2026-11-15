namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The credentials of one external sign-in provider; the provider is enabled when its section exists.
/// </summary>
public sealed class ProviderOptions
{
	#region Properties
	/// <summary>Gets or sets the OAuth client id issued by the provider.</summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>Gets or sets the OAuth client secret issued by the provider; keep it in user secrets or server settings.</summary>
	public string ClientSecret { get; set; } = string.Empty;

	/// <summary>Gets or sets the tenant, for providers that have one (Microsoft); blank selects the provider's default.</summary>
	public string Tenant { get; set; } = string.Empty;
	#endregion
}
