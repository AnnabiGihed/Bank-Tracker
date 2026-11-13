namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Transactions tab shows: the month's payments with their category, split parts and
///              fixed-payment cell, the filters (search, kind, category, not confirmed only) with the count and total
///              of what they leave, and the category pickers. A split payment counts only its parts in the filtered
///              category. Computed as the old page did.
/// </summary>
public sealed class TransactionsViewModel
{
	#region Fields
	/// <summary>The analysis that holds the category tree, for the split editor.</summary>
	private readonly Analysis _analysis;

	/// <summary>The formats of amounts and dates.</summary>
	private readonly Formats _formats;

	/// <summary>Every payment of the month, in the analysis's order.</summary>
	private readonly IReadOnlyList<TransactionRow> _rows;

	/// <summary>The ids of the accepted fixed payments that are subscriptions, whose star opens the Subscriptions tab.</summary>
	private readonly HashSet<string> _subscriptionIds;

	/// <summary>The category the filter is on; <see langword="null"/> for all.</summary>
	private string? _categoryId;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="TransactionsViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="formats">The formats of amounts and dates.</param>
	/// <param name="categoryId">A category to filter on, such as the one a doughnut slice was clicked on, or <see langword="null"/> for all. It
	/// counts only when the month has payments in it; a filter on a category or a search shows every kind of payment.</param>
	/// <param name="search">Text to search for, or <see langword="null"/> for none.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public TransactionsViewModel(Analysis analysis, string month, Formats formats, string? categoryId = null, string? search = null)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		_analysis = analysis;
		_formats = formats;
		_subscriptionIds = analysis.Fixed.Where(f => f.IsSubscription).Select(f => f.Def.Id.Value).ToHashSet(StringComparer.Ordinal);
		var items = analysis.Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal)).ToList();
		_rows = [.. items.Select(i => ToRow(i, formats))];
		var withPayments = items.SelectMany(i => i.Parts).SelectMany(p => CategoryOptions.WithAncestors(analysis, p.Category)).ToHashSet(StringComparer.Ordinal);
		FilterCategories = [.. CategoryOptions.TreeOrder(analysis, CategoryOptions.ByName(formats), c => withPayments.Contains(c.Id.Value))
			.Select(x => CategoryOptions.ToOption(x.Category, x.Depth))];
		_categoryId = categoryId is not null && FilterCategories.Any(c => string.Equals(c.Id, categoryId, StringComparison.Ordinal)) ? categoryId : null;
		Search = search ?? string.Empty;
		Type = _categoryId is not null || Search.Length > 0 ? TransactionTypeFilter.Everything : TransactionTypeFilter.Spending;
		CategoryGroups = CategoryOptions.Grouped(analysis, formats);
		SpendingCategories = CategoryOptions.Spending(analysis, formats);
	}
	#endregion

	#region Properties
	/// <summary>Gets or sets the text to search for in the name, the description, the part labels and the amount.</summary>
	public string Search { get; set; }

	/// <summary>Gets or sets the kind of payment shown.</summary>
	public TransactionTypeFilter Type { get; set; }

	/// <summary>Gets or sets the category to filter on, parents including their sub-categories; <see langword="null"/> for all.</summary>
	public string? CategoryId
	{
		get => _categoryId;
		set => _categoryId = string.IsNullOrEmpty(value) ? null : value;
	}

	/// <summary>Gets or sets a value indicating whether only payments with an unconfirmed category are shown.</summary>
	public bool UnconfirmedOnly { get; set; }

	/// <summary>Gets the categories the filter offers: those with payments (or parts of split payments) in the month and their ancestors, in tree order.</summary>
	public IReadOnlyList<CategoryOption> FilterCategories { get; }

	/// <summary>Gets every category grouped by type, for the picker of a payment.</summary>
	public IReadOnlyList<CategoryGroup> CategoryGroups { get; }

	/// <summary>Gets the spending categories in tree order, the only ones a part of a split payment can use.</summary>
	public IReadOnlyList<CategoryOption> SpendingCategories { get; }

	/// <summary>Gets every payment of the month, whatever the filters.</summary>
	public IReadOnlyList<TransactionRow> AllRows => _rows;

	/// <summary>Gets the payments the filters leave, in the analysis's order.</summary>
	public IReadOnlyList<TransactionRow> VisibleRows
	{
		get
		{
			var text = Search.Trim().ToLowerInvariant();
			return [.. _rows.Where(r => (Type == TransactionTypeFilter.Everything || r.Type == Type)
				&& (_categoryId is null || r.CategoryIds.Contains(_categoryId, StringComparer.Ordinal))
				&& (text.Length == 0 || r.SearchText.Contains(text, StringComparison.Ordinal))
				&& (!UnconfirmedOnly || !r.Confirmed))];
		}
	}

	/// <summary>Gets the words that tell how many payments the filters leave.</summary>
	public string CountText => $"{VisibleRows.Count} transactions";

	/// <summary>Gets what the shown payments add up to: money out as a positive amount. A split payment counts only its
	/// parts in the filtered category.</summary>
	public decimal Total => VisibleRows.Sum(r => _categoryId is not null && r.IsSplit ? r.PartAmountsOut.GetValueOrDefault(_categoryId) : -r.Amount);

	/// <summary>Gets the words that tell the total: money out when it is above zero, money in below.</summary>
	public string TotalText => (Total >= 0 ? "Total out: " : "Total in: ") + _formats.Money(Math.Abs(Total));

	/// <summary>Gets the message shown when the month has no payment.</summary>
	public static string EmptyMessage => "No transactions in this month.";
	#endregion

	#region Public Methods
	/// <summary>Opens the split editor for a payment.</summary>
	/// <param name="payment">The payment to split, or whose split to edit.</param>
	/// <returns>The editor.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="payment"/> is <see langword="null"/>.</exception>
	public SplitEditor OpenSplit(TransactionRow payment)
	{
		ArgumentNullException.ThrowIfNull(payment);
		return new(payment, SpendingCategories, _formats);
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the row of one payment.</summary>
	private TransactionRow ToRow(TxnInfo item, Formats formats)
	{
		var search = $"{item.Name} {item.T.Description} {string.Join(' ', item.Parts.Select(p => p.Label))} {formats.Decimal2(Math.Abs(item.T.Amount))}".ToLowerInvariant();
		var categoryIds = item.Parts.SelectMany(p => CategoryOptions.WithAncestors(_analysis, p.Category)).Distinct(StringComparer.Ordinal).ToList();
		return new(item.T.Id.Value, item.SeriesKey, item.Name, item.T.Description, formats.Day(item.T.Date), item.T.Amount, formats.Money(item.T.Amount), TypeOf(item),
			categoryIds, item.CategoryConfirmed, search, item.IsSplit, item.Category.Id.Value, item.Category.Name,
			item.IsSplit ? [.. item.Parts.Select(p => ToTag(p, formats))] : [], item.IsSplit ? RolledParts(item) : new Dictionary<string, decimal>(StringComparer.Ordinal),
			item.EffectiveType == CategoryType.Expense, FixedMarkOf(item), item.Recurring?.Name, SplitSeedOf(item),
			item.Category.Type == CategoryType.Expense ? item.Category.Id.Value : string.Empty, item.Recurring is { } recurring && _subscriptionIds.Contains(recurring.Id.Value));
	}

	/// <summary>Builds the tag of one part of a split payment.</summary>
	private PartTag ToTag(TxnPart part, Formats formats) => new($"{part.Label ?? part.Category.Name} {formats.Money(Math.Abs(part.Amount))}", part.Category.Color,
		string.Join(" › ", _analysis.Tree.Path(part.Category.Id.Value).Select(c => c.Name)));

	/// <summary>Names the kind of a payment for the type filter: own transfer, saving, income or spending.</summary>
	private static TransactionTypeFilter TypeOf(TxnInfo item)
	{
		if (item.IsOwnMove)
		{
			return TransactionTypeFilter.OwnTransfers;
		}

		if (item.IsSaving)
		{
			return TransactionTypeFilter.Savings;
		}

		return item.T.Amount > 0 ? TransactionTypeFilter.Income : TransactionTypeFilter.Spending;
	}

	/// <summary>Decides what the fixed-payment cell shows: nothing for money in and own transfers, a star when the
	/// payment belongs to a fixed payment, otherwise a button that marks it as one.</summary>
	private static FixedMark FixedMarkOf(TxnInfo item)
	{
		if (item.T.Amount >= 0 || item.IsOwnMove)
		{
			return FixedMark.None;
		}

		return item.Recurring is not null ? FixedMark.IsFixed : FixedMark.CanMark;
	}

	/// <summary>Builds what the split editor starts from when the payment is split: its parts as positive amounts. A
	/// "Difference" part that goes the other way (the payment became smaller than its parts) is left out, so the editor
	/// shows what is too much.</summary>
	private static List<SplitPartSeed>? SplitSeedOf(TxnInfo item) => item.IsSplit
		? [.. item.Parts.Where(p => Math.Sign(p.Amount) == Math.Sign(item.T.Amount)).Select(p => new SplitPartSeed(Math.Abs(p.Amount), p.Category.Id.Value, p.Label))]
		: null;

	/// <summary>Adds up a split payment's parts per category and every ancestor of it, as amounts out, so the category
	/// filter can total only the parts it matches.</summary>
	private Dictionary<string, decimal> RolledParts(TxnInfo item)
	{
		var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
		foreach (var part in item.Parts)
		{
			foreach (var id in CategoryOptions.WithAncestors(_analysis, part.Category))
			{
				totals[id] = totals.GetValueOrDefault(id) - part.Amount;
			}
		}

		return totals;
	}
	#endregion
}
