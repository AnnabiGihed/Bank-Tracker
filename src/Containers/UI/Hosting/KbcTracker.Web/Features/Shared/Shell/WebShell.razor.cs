namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The interactive root of the web app. A cascading value cannot cross from the static page into an
///              interactive component, but a string parameter can, so this component takes the one-time notice as a
///              parameter and cascades it to the layout of the shared <see cref="KbcTrackerRouter"/>, together with the account controls
///              (the signed-in name and the sign-out button) that the layout shows in its header.
/// </summary>
public sealed partial class WebShell : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the one-time message to show, or <see langword="null"/> for none.</summary>
	[Parameter]
	public string? Notice { get; set; }

	/// <summary>Gets or sets the name of the signed-in account, shown in the header.</summary>
	[Parameter]
	public string AccountName { get; set; } = string.Empty;

	/// <summary>Gets or sets the name of the form field that carries the antiforgery token of the sign-out post.</summary>
	[Parameter]
	public string TokenField { get; set; } = string.Empty;

	/// <summary>Gets or sets the antiforgery token of the sign-out post.</summary>
	[Parameter]
	public string Token { get; set; } = string.Empty;
	#endregion
}
