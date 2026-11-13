namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One category as the Categories tab lists it: its place in the tree, its total in the month rolled up
///              through its sub-categories, its bar, its comparison with the previous months and its keywords.
/// </summary>
/// <param name="Id">The category id.</param>
/// <param name="ParentId">The parent's id, or an empty string for a top-level category.</param>
/// <param name="Name">The category name.</param>
/// <param name="Color">The category colour, as <c>#rrggbb</c>.</param>
/// <param name="Depth">The level in the tree, one for a top-level category.</param>
/// <param name="HasChildren">Whether the category has sub-categories, which the row can show or hide.</param>
/// <param name="Type">How the category counts in the totals.</param>
/// <param name="BuiltIn">Whether the app needs the category, so it cannot be deleted or moved.</param>
/// <param name="Keywords">The matching keywords, comma separated.</param>
/// <param name="PaymentCount">How many payments were ever made in it, sub-categories included.</param>
/// <param name="Value">The month's total, sub-categories included.</param>
/// <param name="ValueText">The month's total, formatted, or a dash for none.</param>
/// <param name="BarPercent">The width of the bar, 0 to 100, relative to the largest top-level total of its type.</param>
/// <param name="Comparison">How the total compares with the previous months.</param>
public sealed record CategoryRow(string Id, string ParentId, string Name, string Color, int Depth, bool HasChildren, CategoryType Type, bool BuiltIn,
	string Keywords, int PaymentCount, decimal Value, string ValueText, decimal BarPercent, CategoryComparison Comparison)
{
	#region Properties
	/// <summary>Gets a value indicating whether a sub-category can be added: below the deepest level there is none.</summary>
	public bool CanAddSubCategory => Depth < CategoryTree.MaxDepth;

	/// <summary>Gets the words under the name: how many payments the category holds in total.</summary>
	public string PaymentsText => $"{PaymentCount} payment(s) in total";
	#endregion
}
