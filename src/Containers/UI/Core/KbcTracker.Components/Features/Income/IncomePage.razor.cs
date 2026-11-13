namespace KbcTracker.Components.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Income tab (<c>/income?m=&amp;show=</c>): the month's earnings (salary and other income), refunds and
///              loan money received, each with its payments, a category picker and confirm button like a Transactions
///              row, then the earnings of the last twelve months and the earnings by payer. The <c>show</c> query value
///              (<c>earnings</c>, <c>refunds</c> or <c>loan-received</c>) moves the focus, and so the view, to that
///              section. The figures come from <see cref="IncomeViewModel"/>.
/// </summary>
public partial class IncomePage : TabPageBase
{
	#region Parameters
	/// <summary>Gets or sets the figure whose section is shown first, from the <c>show</c> query value.</summary>
	[SupplyParameterFromQuery(Name = "show")]
	[Parameter]
	public string? ShowQuery { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private IncomeViewModel? _view;

	/// <summary>Every category a payment can be given, in tree order.</summary>
	private IReadOnlyList<CategoryOption> _categories = [];

	/// <summary>The headings of the sections, captured on render, to move the focus to one.</summary>
	private readonly Dictionary<string, ElementReference> _headings = new(StringComparer.Ordinal);

	/// <summary>The <c>show</c> query value the page was last built for.</summary>
	private string? _appliedShow;

	/// <summary>Whether the section named by <c>show</c> still has to receive the focus.</summary>
	private bool _focusPending;
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnParametersSet() => EnsureBuilt(force: !string.Equals(_appliedShow, ShowQuery, StringComparison.Ordinal));

	/// <inheritdoc />
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (_focusPending && _view?.Focus is { } id && _headings.TryGetValue(id, out var heading))
		{
			_focusPending = false;
			await heading.FocusAsync();
		}
	}
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis)
	{
		_focusPending = _focusPending || !string.Equals(_appliedShow, ShowQuery, StringComparison.Ordinal);
		_appliedShow = ShowQuery;
		_view = new IncomeViewModel(analysis, Month, Formats, MonthFigureNames.Parse(ShowQuery));
		_categories = [.. _view.CategoryGroups.SelectMany(g => g.Options)];
	}
	#endregion

	#region Event Handlers
	/// <summary>Sets a payment's category for its whole payer, then offers to limit it to this one payment.</summary>
	/// <param name="row">The payment whose category was changed.</param>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>A task that completes when the decision was sent.</returns>
	private async Task ChangeCategoryAsync(IncomeRow row, string categoryId)
	{
		if (categoryId.Length == 0 || string.Equals(categoryId, row.CategoryId, StringComparison.Ordinal))
		{
			return;
		}

		var response = await DecideAsync(row.ChangeCategoryRequest(categoryId));
		if (response is not null)
		{
			var categoryName = _categories.FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.Ordinal))?.Name ?? categoryId;
			OfferOnlyThisOne(row.Payer, categoryName, row.OnlyThisOneRequest(categoryId, response.PreviousAssignment));
		}
	}
	#endregion
}
