namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The page at the root address: it sends the owner to the Overview tab, so the Android app, which has no
///              server to redirect, starts on the same page as the web app.
/// </summary>
public partial class HomePage : ComponentBase
{
	#region Injected Services
	/// <summary>Gets or sets the navigation manager.</summary>
	[Inject]
	public required NavigationManager Navigation { get; set; }
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnInitialized() => Navigation.NavigateTo(TabRoutes.Overview, replace: true);
	#endregion
}
