namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Tells how a month's spending in a category compares with the average of the previous months.
/// </summary>
public enum ComparisonKind
{
	/// <summary>Nothing to compare, or the category is not spending.</summary>
	None,

	/// <summary>More than 15 percent above the average (red).</summary>
	Higher,

	/// <summary>More than 15 percent below the average (green).</summary>
	Lower,

	/// <summary>Within 15 percent of the average.</summary>
	Similar,
}
