namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The interactive root of the web app. A cascading value cannot cross from the static page into an
///              interactive component, but a string parameter can, so this component takes the one-time notice as a
///              parameter and cascades it to the layout of the shared <see cref="KbcTrackerRouter"/>.
/// </summary>
public sealed partial class WebShell : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the one-time message to show, or <see langword="null"/> for none.</summary>
	[Parameter]
	public string? Notice { get; set; }
	#endregion
}
