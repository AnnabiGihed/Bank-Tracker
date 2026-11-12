namespace KbcTracker.Domain.Features.Categories.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors creating or changing a category can fail with, coded <c>Category.Property.Reason</c>.
/// </summary>
public static class CategoryErrors
{
	#region Static Instances
	/// <summary>
	/// The category name is missing.
	/// </summary>
	public static readonly Error NameRequired = new("Category.Name.Required", "The category needs a name.");

	/// <summary>
	/// The colour is not a <c>#rrggbb</c> value.
	/// </summary>
	public static readonly Error ColorInvalid = new("Category.Color.Invalid", "The colour must look like #3454d1.");

	/// <summary>
	/// A built-in category, which the analysis relies on, cannot be deleted.
	/// </summary>
	public static readonly Error BuiltInProtected = new("Category.Id.Protected", "This category can't be deleted.");

	/// <summary>
	/// A built-in category stays where it is and keeps its type: the analysis relies on both.
	/// </summary>
	public static readonly Error BuiltInFixed = new("Category.Id.Fixed", "A built-in category can be renamed and recoloured, but not moved or given another type.");

	/// <summary>
	/// The category to change or delete does not exist.
	/// </summary>
	public static readonly Error NotFound = new("Category.NotFound", "This category does not exist.");

	/// <summary>
	/// A payment, payee or fixed payment was given a category that does not exist.
	/// </summary>
	public static readonly Error Unknown = new("Category.Unknown", "The chosen category does not exist.");

	/// <summary>
	/// The chosen parent category does not exist.
	/// </summary>
	public static readonly Error ParentNotFound = new("Category.Parent.NotFound", "The parent category does not exist.");

	/// <summary>
	/// The chosen parent is the category itself or one of its own sub-categories.
	/// </summary>
	public static readonly Error ParentCycle = new("Category.Parent.Cycle", "A category can't be placed under itself or under one of its own sub-categories.");

	/// <summary>
	/// The tree would become deeper than the four levels allowed.
	/// </summary>
	public static readonly Error TooDeep = new("Category.Parent.TooDeep", "Categories can have at most four levels.");

	/// <summary>
	/// A sub-category's type differs from the type of its top-level category.
	/// </summary>
	public static readonly Error TypeFollowsRoot = new("Category.Type.FollowsRoot", "A sub-category has the same type as its top-level category.");
	#endregion
}
