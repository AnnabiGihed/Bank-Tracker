namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Shows why the workspace could not be read (access refused, conflict with the backend's own reason, or
///              backend unavailable) with an action to try again. It shows nothing when there is no problem.
/// </summary>
public partial class BackendStatus : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the problem to show, or <see langword="null"/> when the last call succeeded.</summary>
	[Parameter]
	public BackendProblem? Problem { get; set; }

	/// <summary>Gets or sets what happens when the owner asks to try again.</summary>
	[Parameter]
	public EventCallback OnRetry { get; set; }
	#endregion
}
