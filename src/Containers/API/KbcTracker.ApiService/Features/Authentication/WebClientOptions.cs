namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The settings of the web app's confidential client: its secret and extra redirect addresses.
/// </summary>
public sealed class WebClientOptions
{
	#region Properties
	/// <summary>Gets or sets the client secret the web app presents; required, kept in secrets.</summary>
	public string Secret { get; set; } = string.Empty;

	/// <summary>Gets the redirect addresses accepted besides the default <c>https://localhost:5001/signin-oidc</c>.</summary>
	public ICollection<string> ExtraRedirectUris { get; } = [];
	#endregion
}
