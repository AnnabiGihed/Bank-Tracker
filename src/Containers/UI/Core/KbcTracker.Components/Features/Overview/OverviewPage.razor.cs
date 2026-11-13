namespace KbcTracker.Components.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Overview tab (<c>/overview</c>): what needs attention, the five key figures of the month, the last
///              twelve months as a chart, where the month's money went with a drill-down to Transactions, the fixed
///              payments of the month and the biggest expenses. All figures come from <see cref="OverviewViewModel"/>.
/// </summary>
public partial class OverviewPage : TabPageBase
{
	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private OverviewViewModel? _view;
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis) => _view = new OverviewViewModel(analysis, Month, Store.Session, Formats, Store.Today);
	#endregion
}
