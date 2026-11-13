namespace KbcTracker.Components.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Transactions tab (<c>/transactions?m=&amp;cat=&amp;q=</c>): the month's transactions with filters (search,
///              kind, category, not confirmed only), an inline category picker that sets the category of the whole payee
///              and offers "Only this one" as an undo, confirm, mark as fixed and the split dialog (ADR-0007). The filters
///              live in <see cref="TransactionsViewModel"/> and survive a reload after a decision.
/// </summary>
public partial class TransactionsPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets the category to filter on, from the <c>cat</c> query value.</summary>
	[SupplyParameterFromQuery(Name = "cat")]
	[Parameter]
	public string? CategoryQuery { get; set; }

	/// <summary>Gets or sets the text to search for, from the <c>q</c> query value.</summary>
	[SupplyParameterFromQuery(Name = "q")]
	[Parameter]
	public string? SearchQuery { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private TransactionsViewModel? _view;

	/// <summary>The rows that pass the filters.</summary>
	private IReadOnlyList<TransactionRow> _rows = [];

	/// <summary>Every category a transaction can be given, in tree order.</summary>
	private IReadOnlyList<CategoryOption> _categories = [];

	/// <summary>The category query the filters were last set from, to apply a new one only when the address changed.</summary>
	private string? _appliedCategory;

	/// <summary>The search query the filters were last set from.</summary>
	private string? _appliedSearch;
	#endregion

	#region Properties
	/// <summary>Gets the kinds of transaction the filter offers.</summary>
	private static IReadOnlyList<TypeChoice> TypeChoices { get; } =
	[
		new(TransactionTypeFilter.Spending, "Spending"),
		new(TransactionTypeFilter.Income, "Income"),
		new(TransactionTypeFilter.Savings, "Savings"),
		new(TransactionTypeFilter.OwnTransfers, "Own transfers"),
		new(TransactionTypeFilter.Everything, "Everything"),
	];

	/// <summary>Gets the accessible name of the kind filter.</summary>
	private static IReadOnlyDictionary<string, object> TypeInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-label"] = "Kind of transaction" };
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		EnsureBuilt(force: AddressChanged());
	}
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis)
	{
		var previous = _view;
		var fromAddress = previous is null || AddressChanged();
		_appliedCategory = CategoryQuery;
		_appliedSearch = SearchQuery;
		var view = new TransactionsViewModel(analysis, Month, Formats, CategoryQuery, SearchQuery);
		if (!fromAddress && previous is not null)
		{
			view.Search = previous.Search;
			view.Type = previous.Type;
			view.CategoryId = previous.CategoryId;
			view.UnconfirmedOnly = previous.UnconfirmedOnly;
		}

		_view = view;
		_categories = [.. view.CategoryGroups.SelectMany(g => g.Options)];
		ApplyFilters();
	}
	#endregion

	#region Event Handlers
	/// <summary>Filters on the text the owner typed.</summary>
	/// <param name="text">The search text.</param>
	private void OnSearchChanged(string text)
	{
		_view!.Search = text ?? string.Empty;
		ApplyFilters();
	}

	/// <summary>Filters on the kind the owner picked.</summary>
	/// <param name="value">The picked <see cref="TransactionTypeFilter"/>.</param>
	private void OnTypeChanged(object value)
	{
		if (value is TransactionTypeFilter type)
		{
			_view!.Type = type;
			ApplyFilters();
		}
	}

	/// <summary>Filters on the category the owner picked, or on all categories when cleared.</summary>
	/// <param name="categoryId">The category id, or an empty string.</param>
	private void OnCategoryFilterChanged(string categoryId)
	{
		_view!.CategoryId = categoryId;
		ApplyFilters();
	}

	/// <summary>Shows only the payments whose category is not confirmed, or all of them.</summary>
	/// <param name="unconfirmedOnly">Whether to show the unconfirmed payments only.</param>
	private void OnUnconfirmedChanged(bool unconfirmedOnly)
	{
		_view!.UnconfirmedOnly = unconfirmedOnly;
		ApplyFilters();
	}

	/// <summary>Sets a payment's category for its whole payee, then offers to limit it to this one payment.</summary>
	/// <param name="row">The payment whose category was changed.</param>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>A task that completes when the decision was sent.</returns>
	private async Task ChangeCategoryAsync(TransactionRow row, string categoryId)
	{
		if (categoryId.Length == 0 || string.Equals(categoryId, row.CategoryId, StringComparison.Ordinal))
		{
			return;
		}

		var response = await DecideAsync(row.ChangeCategoryRequest(categoryId));
		if (response is not null)
		{
			var categoryName = _categories.FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.Ordinal))?.Name ?? categoryId;
			OfferOnlyThisOne(row.Name, categoryName, row.OnlyThisOneRequest(categoryId, response.PreviousAssignment));
		}
	}

	/// <summary>Opens the editor that splits a payment into parts and sends what it returns.</summary>
	/// <param name="row">The payment to split.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task OpenSplitAsync(TransactionRow row)
	{
		var editor = _view!.OpenSplit(row);
		object? result = await Dialogs.OpenAsync<SplitDialog>($"Split {row.Name}", new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SplitDialog.Editor)] = editor },
			new DialogOptions { Width = "720px", CloseDialogOnOverlayClick = false });
		if (result is DecisionRequest request)
		{
			await DecideAsync(request);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Determines whether the address asks for other filters than the ones last applied.</summary>
	private bool AddressChanged() => !string.Equals(_appliedCategory, CategoryQuery, StringComparison.Ordinal)
		|| !string.Equals(_appliedSearch, SearchQuery, StringComparison.Ordinal);

	/// <summary>Recalculates the rows that pass the filters.</summary>
	private void ApplyFilters() => _rows = _view!.VisibleRows;

	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : One entry of the kind filter.
	/// </summary>
	/// <param name="Value">The kind.</param>
	/// <param name="Label">The text shown for it.</param>
	private sealed record TypeChoice(TransactionTypeFilter Value, string Label);
	#endregion
}
