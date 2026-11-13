namespace KbcTracker.Components.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The To review tab (<c>/review</c>): the payees whose category was chosen automatically, so the owner
///              confirms it (✓), picks another one, or confirms every payee shown after filtering on money out or in. A
///              decision applies to all payments of the payee, past and future. The tab is not tied to a month.
/// </summary>
public partial class ReviewPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private ReviewViewModel? _view;

	/// <summary>The payees that pass the direction filter.</summary>
	private IReadOnlyList<ReviewRow> _rows = [];

	/// <summary>Every category a payee can be given, in tree order.</summary>
	private IReadOnlyList<CategoryOption> _categories = [];
	#endregion

	#region Properties
	/// <summary>Gets the directions the filter offers.</summary>
	private static IReadOnlyList<DirectionChoice> DirectionChoices { get; } =
	[
		new(DirectionFilter.All, "All payees"),
		new(DirectionFilter.Out, "Money out"),
		new(DirectionFilter.In, "Money in"),
	];

	/// <summary>Gets the accessible name of the direction filter.</summary>
	private static IReadOnlyDictionary<string, object> DirectionInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-label"] = "Direction" };
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis)
	{
		var direction = _view?.Direction ?? DirectionFilter.All;
		_view = new ReviewViewModel(analysis, Formats) { Direction = direction };
		_categories = [.. _view.CategoryGroups.SelectMany(g => g.Options)];
		_rows = _view.VisibleRows;
	}
	#endregion

	#region Event Handlers
	/// <summary>Filters the payees on the direction the owner picked.</summary>
	/// <param name="value">The picked <see cref="DirectionFilter"/>.</param>
	private void OnDirectionChanged(object value)
	{
		if (value is DirectionFilter direction)
		{
			_view!.Direction = direction;
			_rows = _view.VisibleRows;
		}
	}

	/// <summary>Sets the category of a payee, for all its payments.</summary>
	/// <param name="row">The payee.</param>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>A task that completes when the decision was sent.</returns>
	private async Task ChangeCategoryAsync(ReviewRow row, string categoryId)
	{
		if (categoryId.Length > 0 && !string.Equals(categoryId, row.CategoryId, StringComparison.Ordinal))
		{
			await DecideAsync(row.ChangeCategoryRequest(categoryId));
		}
	}

	/// <summary>Asks to confirm every payee shown and, when the owner agrees, sends one decision for all of them.</summary>
	/// <returns>A task that completes when the owner answered and the decision was sent.</returns>
	private async Task ConfirmShownAsync()
	{
		var confirmed = await Dialogs.Confirm(_view!.ConfirmShownPrompt(), "Confirm all shown", new ConfirmOptions { OkButtonText = "Confirm", CancelButtonText = "Cancel" });
		if (confirmed == true)
		{
			await DecideAsync(_view.ConfirmShownRequest());
		}
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : One entry of the direction filter.
	/// </summary>
	/// <param name="Value">The direction.</param>
	/// <param name="Label">The text shown for it.</param>
	private sealed record DirectionChoice(DirectionFilter Value, string Label);
	#endregion
}
