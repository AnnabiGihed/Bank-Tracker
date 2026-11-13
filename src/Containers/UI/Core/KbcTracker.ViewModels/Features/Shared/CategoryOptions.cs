namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Lists categories for pickers and trees: in tree order (the top-level categories, each followed by its
///              sub-categories, level by level), grouped by type, each with its level. Moved from the old page kit.
/// </summary>
public static class CategoryOptions
{
	#region Public Methods
	/// <summary>Lists categories in tree order.</summary>
	/// <param name="analysis">The analysis that holds the category tree.</param>
	/// <param name="order">Orders the categories that share a parent (and the top-level ones).</param>
	/// <param name="include">Keeps only the categories it accepts, or <see langword="null"/> for all; a category that
	/// is left out takes its sub-categories with it, so include a category's ancestors too.</param>
	/// <returns>Each category with its level, one for a top-level category.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="order"/> is <see langword="null"/>.</exception>
	public static IReadOnlyList<(Category Category, int Depth)> TreeOrder(Analysis analysis, Func<IEnumerable<Category>, IEnumerable<Category>> order,
		Func<Category, bool>? include = null)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(order);
		var result = new List<(Category, int)>();
		Walk(order(analysis.Tree.Roots()), 1);
		return result;

		void Walk(IEnumerable<Category> siblings, int depth)
		{
			foreach (var category in siblings.Where(c => include is null || include(c)))
			{
				result.Add((category, depth));
				Walk(order(analysis.Tree.Children(category.Id.Value)), depth + 1);
			}
		}
	}

	/// <summary>Orders categories by name, the sibling order of the pickers.</summary>
	/// <param name="formats">The formats whose culture decides the order.</param>
	/// <returns>A function that orders categories by name.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> is <see langword="null"/>.</exception>
	public static Func<IEnumerable<Category>, IEnumerable<Category>> ByName(Formats formats)
	{
		ArgumentNullException.ThrowIfNull(formats);
		return categories => categories.OrderBy(c => c.Name, formats.NameComparer);
	}

	/// <summary>Lists every category grouped by type, in tree order, as the category picker of a payment shows them.</summary>
	/// <param name="analysis">The analysis that holds the category tree.</param>
	/// <param name="formats">The formats whose culture decides the order of siblings.</param>
	/// <returns>The groups, ordered by type.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static IReadOnlyList<CategoryGroup> Grouped(Analysis analysis, Formats formats) =>
		[.. TreeOrder(analysis, ByName(formats)).GroupBy(x => x.Category.Type).OrderBy(g => g.Key)
			.Select(g => new CategoryGroup(TypeLabel(g.Key), g.Key, [.. g.Select(x => ToOption(x.Category, x.Depth))]))];

	/// <summary>Lists the spending categories in tree order, the only ones a part of a split payment can use.</summary>
	/// <param name="analysis">The analysis that holds the category tree.</param>
	/// <param name="formats">The formats whose culture decides the order of siblings.</param>
	/// <returns>The spending categories with their level.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static IReadOnlyList<CategoryOption> Spending(Analysis analysis, Formats formats) =>
		[.. TreeOrder(analysis, ByName(formats), c => c.Type == CategoryType.Expense).Select(x => ToOption(x.Category, x.Depth))];

	/// <summary>Lists a category's id followed by its ancestors' ids.</summary>
	/// <param name="analysis">The analysis that holds the category tree.</param>
	/// <param name="category">The category.</param>
	/// <returns>The ids, the category's own first.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="category"/> is <see langword="null"/>.</exception>
	public static IEnumerable<string> WithAncestors(Analysis analysis, Category category)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(category);
		return analysis.Tree.Ancestors(category.Id.Value).Select(x => x.Id.Value).Prepend(category.Id.Value);
	}

	/// <summary>Creates the picker entry of a category.</summary>
	/// <param name="category">The category.</param>
	/// <param name="depth">Its level, one for a top-level category.</param>
	/// <returns>The entry.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="category"/> is <see langword="null"/>.</exception>
	public static CategoryOption ToOption(Category category, int depth)
	{
		ArgumentNullException.ThrowIfNull(category);
		return new(category.Id.Value, category.Name, depth, category.Type, category.Color);
	}

	/// <summary>Gets the heading of a category type.</summary>
	/// <param name="type">The category type.</param>
	/// <returns>The heading shown to the user.</returns>
	public static string TypeLabel(CategoryType type) => type switch
	{
		CategoryType.Expense => "Spending",
		CategoryType.Income => "Income",
		CategoryType.Savings => "Savings",
		_ => "Transfer",
	};
	#endregion
}
