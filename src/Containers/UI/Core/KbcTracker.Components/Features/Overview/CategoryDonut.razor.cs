namespace KbcTracker.Components.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The doughnut of where the month's money went. It starts at the top-level categories; a slice with
///              sub-categories drills down (the path above is a breadcrumb to go back up), and a slice without them opens
///              Transactions filtered on that category. The list of slices below the chart does the same with buttons, so
///              the drill-down also works with the keyboard and a screen reader.
/// </summary>
public partial class CategoryDonut : ComponentBase
{
	#region Injected Services
	/// <summary>Gets or sets the navigation manager.</summary>
	[Inject]
	public required NavigationManager Navigation { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets the doughnut's data for the month.</summary>
	[EditorRequired]
	[Parameter]
	public required CategoryDrillDown DrillDown { get; set; }

	/// <summary>Gets or sets the month as <c>yyyy-MM</c>, kept in the link to Transactions.</summary>
	[Parameter]
	public string Month { get; set; } = string.Empty;
	#endregion

	#region Fields
	/// <summary>The category the owner drilled into, or empty for the top level.</summary>
	private string _at = string.Empty;

	/// <summary>The drill-down the slices were built from, to start over when the data changes.</summary>
	private CategoryDrillDown? _builtFrom;

	/// <summary>The breadcrumb of the path to the current level.</summary>
	private IReadOnlyList<Crumb> _crumbs = [];

	/// <summary>The slices of the current level.</summary>
	private IReadOnlyList<CategorySlice> _slices = [];

	/// <summary>The colour of each slice, in the same order.</summary>
	private IReadOnlyList<string> _fills = [];
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		if (!ReferenceEquals(_builtFrom, DrillDown))
		{
			_builtFrom = DrillDown;
			_at = string.Empty;
		}

		Build();
	}
	#endregion

	#region Event Handlers
	/// <summary>Reacts to a click on a slice of the chart.</summary>
	/// <param name="args">What was clicked.</param>
	private void OnSliceClick(SeriesClickEventArgs args)
	{
		if (args.Data is CategorySlice slice)
		{
			Activate(slice);
		}
	}

	/// <summary>Drills into a slice that has sub-categories, or opens the Transactions of one that has none.</summary>
	/// <param name="slice">The slice the owner chose.</param>
	private void Activate(CategorySlice slice)
	{
		if (slice.CanDrill)
		{
			GoTo(slice.Id);
			return;
		}

		Navigation.NavigateTo(TabRoutes.TransactionsOf(Month, slice.Id));
	}

	/// <summary>Shows the level below, or above, a category.</summary>
	/// <param name="categoryId">The category to show the children of, or empty for the top level.</param>
	private void GoTo(string categoryId)
	{
		_at = categoryId;
		Build();
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the slices and the breadcrumb of the current level.</summary>
	private void Build()
	{
		_slices = DrillDown.Slices(_at);
		_fills = [.. _slices.Select(s => s.Color)];
		_crumbs = DrillDown.Path(_at);
	}
	#endregion
}
