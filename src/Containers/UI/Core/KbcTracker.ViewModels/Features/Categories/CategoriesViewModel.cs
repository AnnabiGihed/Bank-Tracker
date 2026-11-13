namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Categories tab shows: the category tree per type with the month's totals rolled up through
///              the tree, amount bars and the comparison with the previous months, which rows are expanded, and the
///              forms to create, move, edit and delete categories.
/// </summary>
public sealed class CategoriesViewModel
{
	#region Constants
	/// <summary>How many of the months before the selected one the comparison averages.</summary>
	private const int ComparedMonths = 3;

	/// <summary>The colour a new top-level category starts with.</summary>
	private const string NewCategoryColor = "#3454d1";
	#endregion

	#region Fields
	/// <summary>The analysis whose category tree the forms read.</summary>
	private readonly Analysis _analysis;

	/// <summary>The categories in the order the tab lists them, for the parent picker of the forms.</summary>
	private readonly IReadOnlyList<(Category Category, int Depth)> _ordered;

	/// <summary>The rows whose sub-categories are shown.</summary>
	private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="CategoriesViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="formats">The formats of amounts and months.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public CategoriesViewModel(Analysis analysis, string month, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		_analysis = analysis;
		MonthLabel = Formats.MonthName(month);
		var rolled = RolledTotals(analysis, analysis.Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal)));
		var previous = analysis.AvailableMonths.Where(m => string.CompareOrdinal(m, month) < 0).Take(ComparedMonths)
			.Select(m => RolledTotals(analysis, analysis.Items.Where(i => string.Equals(i.Month, m, StringComparison.Ordinal)))).ToList();
		var counts = analysis.Items.SelectMany(i => i.Parts.Select(p => p.Category.Id.Value).Distinct(StringComparer.Ordinal))
			.GroupBy(id => id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (decimal)g.Count(), StringComparer.Ordinal);
		var payments = analysis.Tree.RollUp(counts);
		_ordered = CategoryOptions.TreeOrder(analysis,
			siblings => siblings.OrderByDescending(c => rolled.GetValueOrDefault(c.Id.Value)).ThenBy(c => c.Name, formats.NameComparer));
		Cards = [.. _ordered.GroupBy(x => analysis.Tree.Root(x.Category.Id.Value)!.Type).OrderBy(g => g.Key)
			.Select(g => BuildCard(g.Key, [.. g], rolled, previous, payments, formats))];
	}
	#endregion

	#region Properties
	/// <summary>Gets the selected month's English name and year.</summary>
	public string MonthLabel { get; }

	/// <summary>Gets one card per category type that has categories.</summary>
	public IReadOnlyList<CategoryTypeCard> Cards { get; }
	#endregion

	#region Public Methods
	/// <summary>Tells whether a category's sub-categories are shown.</summary>
	/// <param name="categoryId">The category.</param>
	/// <returns><see langword="true"/> when its row is expanded.</returns>
	public bool IsExpanded(string categoryId) => _expanded.Contains(categoryId);

	/// <summary>Shows or hides a category's sub-categories.</summary>
	/// <param name="categoryId">The category.</param>
	public void Toggle(string categoryId)
	{
		if (!_expanded.Remove(categoryId))
		{
			_expanded.Add(categoryId);
		}
	}

	/// <summary>Lists the rows of a card that are shown: a top-level category always, a sub-category when every
	/// ancestor above it is shown and its parent is expanded. Sub-categories start hidden.</summary>
	/// <param name="card">The card.</param>
	/// <returns>The visible rows, in tree order.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="card"/> is <see langword="null"/>.</exception>
	public IReadOnlyList<CategoryRow> VisibleRows(CategoryTypeCard card)
	{
		ArgumentNullException.ThrowIfNull(card);
		var shown = new Dictionary<string, bool>(StringComparer.Ordinal);
		var rows = new List<CategoryRow>();
		foreach (var row in card.Rows)
		{
			var visible = row.ParentId.Length == 0 || (shown.GetValueOrDefault(row.ParentId) && _expanded.Contains(row.ParentId));
			shown[row.Id] = visible;
			if (visible)
			{
				rows.Add(row);
			}
		}

		return rows;
	}

	/// <summary>Opens the form for a new category.</summary>
	/// <param name="parentId">The parent, or <see langword="null"/> for a top-level category.</param>
	/// <returns>The form; a sub-category starts with its parent's colour.</returns>
	/// <exception cref="ArgumentException">The parent is not a known category.</exception>
	public CategoryEdit NewCategory(string? parentId = null)
	{
		if (string.IsNullOrEmpty(parentId))
		{
			return new(null, "New category", string.Empty, CategoryType.Expense, string.Empty, NewCategoryColor, string.Empty, builtIn: false, ParentOptions(null), null);
		}

		var parent = _analysis.CategoryById(parentId);
		return !_analysis.Tree.Contains(parentId)
			? throw new ArgumentException("The parent is not a known category.", nameof(parentId))
			: new(null, "New sub-category", string.Empty, parent.Type, parentId, parent.Color, string.Empty, builtIn: false, ParentOptions(null), null);
	}

	/// <summary>Opens the form for an existing category.</summary>
	/// <param name="row">The category's row.</param>
	/// <returns>The form, filled with the category's values.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
	public CategoryEdit Edit(CategoryRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		var parentName = row.ParentId.Length == 0 ? null : _analysis.CategoryById(row.ParentId).Name;
		return new(row.Id, "Edit category", row.Name, row.Type, row.ParentId, row.Color, row.Keywords, row.BuiltIn, ParentOptions(row.Id), parentName);
	}
	#endregion

	#region Private Helpers
	/// <summary>Adds up what the given transactions amount to per category, including everything below it: spending and
	/// savings as positive amounts, income as positive income. A split payment counts each part in its own category.</summary>
	private static IReadOnlyDictionary<string, decimal> RolledTotals(Analysis analysis, IEnumerable<TxnInfo> items) =>
		analysis.Tree.RollUp(items.SelectMany(i => i.Parts).GroupBy(p => p.Category.Id.Value, StringComparer.Ordinal).ToDictionary(
			g => g.Key,
			g => g.Sum(p => p.Category.Type == CategoryType.Income ? p.Amount : -p.Amount),
			StringComparer.Ordinal));

	/// <summary>Builds the card of one category type: its heading with the total of the top-level categories and its rows.</summary>
	private CategoryTypeCard BuildCard(CategoryType type, List<(Category Category, int Depth)> rows, IReadOnlyDictionary<string, decimal> month,
		List<IReadOnlyDictionary<string, decimal>> previous, IReadOnlyDictionary<string, decimal> payments, Formats formats)
	{
		var tops = rows.Where(x => x.Depth == 1).Select(x => month.GetValueOrDefault(x.Category.Id.Value)).ToList();
		var max = tops.DefaultIfEmpty(0).Max();
		var title = type switch { CategoryType.Expense => "Spending", CategoryType.Income => "Income", CategoryType.Savings => "Savings", _ => "Own transfers (not counted)" };
		var cardRows = rows.Select(x =>
		{
			var id = x.Category.Id.Value;
			decimal? average = previous.Count == 0 ? null : previous.Average(p => p.GetValueOrDefault(id));
			return ToRow(x.Category, x.Depth, month.GetValueOrDefault(id), average, (int)payments.GetValueOrDefault(id), max, type, formats);
		});
		return new(type, title, tops.Sum(), formats.Money(tops.Sum()), [.. cardRows]);
	}

	/// <summary>Builds the row of one category.</summary>
	private CategoryRow ToRow(Category category, int depth, decimal value, decimal? average, int paymentCount, decimal max, CategoryType type, Formats formats) => new(
		category.Id.Value, _analysis.Tree.Parent(category.Id.Value)?.Id.Value ?? string.Empty, category.Name, category.Color, depth,
		_analysis.Tree.Children(category.Id.Value).Count > 0, category.Type, category.BuiltIn, string.Join(", ", category.Keywords), paymentCount, value,
		value == 0 ? "—" : formats.Money(value), max <= 0 || value <= 0 ? 0 : value / max * 100, CategoryComparison.Of(value, average, type, formats));

	/// <summary>Lists the categories a category can move under: not itself or anything below it, and not so deep that its
	/// own sub-categories would pass the deepest level. A new category has no sub-categories.</summary>
	private IReadOnlyList<CategoryOption> ParentOptions(string? id)
	{
		var height = id is null ? 1 : Height(id);
		return [.. _ordered.Where(x => (id is null || !IsSelfOrBelow(x.Category.Id.Value, id)) && x.Depth + height <= CategoryTree.MaxDepth)
			.Select(x => CategoryOptions.ToOption(x.Category, x.Depth))];
	}

	/// <summary>Counts the levels of a category's subtree, itself included.</summary>
	private int Height(string id) => 1 + _analysis.Tree.Children(id).Select(c => Height(c.Id.Value)).DefaultIfEmpty(0).Max();

	/// <summary>Tells whether a category is the given ancestor or lies below it.</summary>
	private bool IsSelfOrBelow(string id, string ancestorId) =>
		string.Equals(id, ancestorId, StringComparison.Ordinal) || _analysis.Tree.Ancestors(id).Any(a => string.Equals(a.Id.Value, ancestorId, StringComparison.Ordinal));
	#endregion
}
