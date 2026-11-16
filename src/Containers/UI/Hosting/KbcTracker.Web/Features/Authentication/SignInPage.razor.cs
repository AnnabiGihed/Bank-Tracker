namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The sign-in page, rendered statically on the server (no circuit, no script): one link per provider the host
///              offers, each starting the sign-in at the API with that provider, and a notice when the last attempt failed.
/// </summary>
public sealed partial class SignInPage : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the names of the providers to offer.</summary>
	[Parameter]
	public IReadOnlyList<string> Providers { get; set; } = [];

	/// <summary>Gets or sets the local page to return to after signing in.</summary>
	[Parameter]
	public string ReturnUrl { get; set; } = "/";

	/// <summary>Gets or sets a value indicating whether the last sign-in attempt failed.</summary>
	[Parameter]
	public bool Failed { get; set; }
	#endregion

	#region Private Helpers
	/// <summary>Builds the address that starts the sign-in with one provider.</summary>
	private string StartAddress(string provider) =>
		$"{WebAuthentication.SignInStartPath}?provider={Uri.EscapeDataString(provider)}&{WebAuthentication.ReturnUrlParameter}={Uri.EscapeDataString(ReturnUrl)}";
	#endregion
}
