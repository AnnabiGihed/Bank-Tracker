using System.Globalization;

namespace KbcTracker.Components.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Categories tab (<c>/categories</c>): the category tree per type with the month's roll-up totals, an
///              amount bar and a comparison with the previous months, rows that expand and collapse, a link to the
///              category's transactions, and an edit dialog for creating, changing, moving and deleting categories
///              (ADR-0006). Which rows are expanded survives a reload after a decision.
/// </summary>
public partial class CategoriesPage : TabPageBase
{
	#region Constants
	/// <summary>The width of the expand button in rem, which the text under a name is indented by.</summary>
	private const double ToggleWidthRem = 1.6;

	/// <summary>The indent of each tree level in rem.</summary>
	private const double LevelIndentRem = 1.25;
	#endregion

	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private CategoriesViewModel? _view;
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis)
	{
		var previous = _view;
		var view = new CategoriesViewModel(analysis, Month, Formats);
		if (previous is not null)
		{
			foreach (var id in view.Cards.SelectMany(c => c.Rows).Select(r => r.Id).Where(previous.IsExpanded))
			{
				view.Toggle(id);
			}
		}

		_view = view;
	}
	#endregion

	#region Event Handlers
	/// <summary>Shows or hides the sub-categories of a row.</summary>
	/// <param name="row">The row whose expand button was clicked.</param>
	private void Toggle(CategoryRow row) => _view!.Toggle(row.Id);

	/// <summary>Opens the category dialog and sends what it returns.</summary>
	/// <param name="edit">The category to create or change.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task EditAsync(CategoryEdit edit)
	{
		object? result = await Dialogs.OpenAsync<CategoryDialog>(edit.Title, new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(CategoryDialog.Edit)] = edit },
			new DialogOptions { Width = "520px", CloseDialogOnOverlayClick = false });
		if (result is DecisionRequest request)
		{
			await DecideAsync(request);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the style that indents a row by its level in the tree.</summary>
	private static string IndentStyle(CategoryRow row, double extraRem) =>
		string.Create(CultureInfo.InvariantCulture, $"padding-left: {((row.Depth - 1) * LevelIndentRem) + extraRem}rem");

	/// <summary>Builds the style of a row's amount bar: its width as a share of the largest top-level category, in the category's colour.</summary>
	private static string BarStyle(CategoryRow row) =>
		string.Create(CultureInfo.InvariantCulture, $"width: {Math.Round(row.BarPercent):0}%; background: {row.Color}");

	/// <summary>Names the style class of a comparison with the previous months.</summary>
	private static string ComparisonClass(CategoryComparison comparison) => comparison.Kind switch
	{
		ComparisonKind.Higher => "bad",
		ComparisonKind.Lower => "ok",
		_ => "nil",
	};
	#endregion
}
