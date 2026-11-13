using Microsoft.AspNetCore.Components.Web;

namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The HTML shell of the web app, rendered statically on every request: the style bundle, the Blazor and
///              Radzen scripts and the interactive shell. It is the only place that still sees the request, so it reads the
///              one-time <see cref="PageNotice"/> cookie here (which also clears it) and passes the message on.
/// </summary>
public sealed partial class App : ComponentBase
{
	#region Static Instances
	/// <summary>Interactive Server without prerendering: the workspace is loaded once, by the circuit, not twice.</summary>
	private static readonly InteractiveServerRenderMode InteractiveRender = new(prerender: false);
	#endregion

	#region Fields
	/// <summary>The message of a failed action that redirected here, or <see langword="null"/> for none.</summary>
	private string? _notice;
	#endregion

	#region Parameters
	/// <summary>Gets or sets the request of this page, available only while the shell is rendered statically.</summary>
	[CascadingParameter]
	public HttpContext? HttpContext { get; set; }
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnInitialized() => _notice = HttpContext is null ? null : PageNotice.Take(HttpContext);
	#endregion
}
