namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the web app adds to the shared header: who is signed in and a sign-out button. The button is a plain
///              form that posts to the sign-out address with the antiforgery token the static page created, because a
///              circuit without prerendering has no request to make one from.
/// </summary>
public sealed partial class AccountMenu : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the name of the signed-in account.</summary>
	[Parameter]
	public string UserName { get; set; } = string.Empty;

	/// <summary>Gets or sets the name of the form field that carries the antiforgery token.</summary>
	[Parameter]
	public string TokenField { get; set; } = string.Empty;

	/// <summary>Gets or sets the antiforgery token for the sign-out post.</summary>
	[Parameter]
	public string Token { get; set; } = string.Empty;
	#endregion
}
