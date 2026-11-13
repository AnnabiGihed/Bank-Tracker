namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The tab bar: one link per tab that keeps the selected month, the tab on show marked with
///              <c>aria-current</c>, and a badge with the number of open items on the tabs that have one.
/// </summary>
public partial class TabBar : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the tabs to show.</summary>
	[Parameter]
	public IReadOnlyList<TabLink> Tabs { get; set; } = [];

	/// <summary>Gets or sets the id of the tab on show.</summary>
	[Parameter]
	public string CurrentTab { get; set; } = string.Empty;

	/// <summary>Gets or sets the selected month as <c>yyyy-MM</c>, kept in every link.</summary>
	[Parameter]
	public string Month { get; set; } = string.Empty;
	#endregion

	#region Private Helpers
	/// <summary>Tells whether a tab is the one on show.</summary>
	private bool IsCurrent(TabLink tab) => string.Equals(tab.Id, CurrentTab, StringComparison.Ordinal);
	#endregion
}
