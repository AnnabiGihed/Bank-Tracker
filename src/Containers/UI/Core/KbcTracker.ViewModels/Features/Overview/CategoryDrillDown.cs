namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The data of the doughnut of where the month's money went: it starts at the top-level spending categories
///              and drills down to a category's sub-categories. Each slice is the category's own spending plus
///              everything below it; below the top level the slices take colours from a palette, because sub-categories
///              share their root's colour, and what no sub-category claims is shown as "not specified".
/// </summary>
public sealed class CategoryDrillDown
{
	#region Constants
	/// <summary>The colour of the part of a category that no sub-category claims.</summary>
	private const string NotSpecifiedColor = "#a0a7b4";
	#endregion

	#region Static Instances
	/// <summary>The colours of the slices below the top level, in order.</summary>
	private static readonly string[] Palette =
	[
		"#3454d1", "#f2994a", "#27ae60", "#9b51e0", "#eb5757", "#2d9cdb", "#f2c94c", "#e84393", "#00cec9", "#6c5ce7", "#e17055", "#16a085",
	];
	#endregion

	#region Fields
	/// <summary>The analysis that holds the category tree.</summary>
	private readonly Analysis _analysis;

	/// <summary>What each spending category spent itself in the month.</summary>
	private readonly Dictionary<string, decimal> _own;

	/// <summary>What each spending category and everything below it spent in the month.</summary>
	private readonly IReadOnlyDictionary<string, decimal> _total;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="CategoryDrillDown"/>.</summary>
	/// <param name="analysis">The analysis that holds the category tree.</param>
	/// <param name="month">The month's figures.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="month"/> is <see langword="null"/>.</exception>
	public CategoryDrillDown(Analysis analysis, MonthSummary month)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		_analysis = analysis;
		var spending = analysis.CategoryList.Where(c => analysis.Tree.Root(c.Id.Value)?.Type == CategoryType.Expense).Select(c => c.Id.Value).ToHashSet(StringComparer.Ordinal);
		_own = month.ByCategory.Where(kv => kv.Value > 0 && spending.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
		_total = analysis.Tree.RollUp(_own);
	}
	#endregion

	#region Public Methods
	/// <summary>Lists the slices of one level, largest first.</summary>
	/// <param name="at">The category whose sub-categories to show, or <see langword="null"/> or empty for the top level.</param>
	/// <returns>The slices; below the top level the part of the category that no sub-category claims comes last.</returns>
	public IReadOnlyList<CategorySlice> Slices(string? at)
	{
		var level = string.IsNullOrEmpty(at);
		var children = level ? _analysis.Tree.Roots().Where(c => c.Type == CategoryType.Expense) : _analysis.Tree.Children(at!);
		var slices = children.Where(c => Total(c.Id.Value) > 0)
			.Select(c => new CategorySlice(c.Id.Value, c.Name, c.Color, Cents(Total(c.Id.Value)), CanDrill(c.Id.Value)))
			.OrderByDescending(s => s.Value).ToList();
		if (level)
		{
			return slices;
		}

		var colored = slices.Select((s, i) => s with { Color = Palette[i % Palette.Length] }).ToList();
		if (_own.GetValueOrDefault(at!) > 0)
		{
			colored.Add(new CategorySlice(at!, _analysis.CategoryById(at!).Name + " – not specified", NotSpecifiedColor, Cents(_own[at!]), CanDrill: false));
		}

		return colored;
	}

	/// <summary>Lists the path from "All" down to a category.</summary>
	/// <param name="at">The category shown now, or <see langword="null"/> or empty for the top level.</param>
	/// <returns>"All" first and the category last, which is the current level.</returns>
	public IReadOnlyList<Crumb> Path(string? at)
	{
		IReadOnlyList<Category> path = string.IsNullOrEmpty(at) ? [] : _analysis.Tree.Path(at);
		var crumbs = new List<Crumb> { new(string.Empty, "All", path.Count == 0) };
		crumbs.AddRange(path.Select((c, i) => new Crumb(c.Id.Value, c.Name, i == path.Count - 1)));
		return crumbs;
	}
	#endregion

	#region Private Helpers
	/// <summary>Gets what a category and everything below it spent.</summary>
	private decimal Total(string id) => _total.GetValueOrDefault(id);

	/// <summary>Tells whether a category has sub-categories with spending.</summary>
	private bool CanDrill(string id) => _analysis.Tree.Children(id).Any(c => Total(c.Id.Value) > 0);

	/// <summary>Rounds an amount to the cent.</summary>
	private static decimal Cents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
	#endregion
}
