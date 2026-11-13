namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One entry of a category picker: a category with its level in the tree, so the picker can show top-level categories as headings and indent the rest.
/// </summary>
/// <param name="Id">The category id.</param>
/// <param name="Name">The category name.</param>
/// <param name="Depth">The level in the tree, one for a top-level category.</param>
/// <param name="Type">How the category counts in the totals.</param>
/// <param name="Color">The category colour, as <c>#rrggbb</c>.</param>
public sealed record CategoryOption(string Id, string Name, int Depth, CategoryType Type, string Color)
{
	#region Properties
	/// <summary>Gets a value indicating whether the category is top level, which the picker shows as a heading.</summary>
	public bool IsRoot => Depth <= 1;
	#endregion
}
