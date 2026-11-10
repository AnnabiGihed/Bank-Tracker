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
	/// The built-in fallback category cannot be deleted.
	/// </summary>
	public static readonly Error OtherCannotBeDeleted = new("Category.Id.Protected", "This category can't be deleted.");
	#endregion
}
