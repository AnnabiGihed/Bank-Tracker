namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a <see cref="CategoryId"/> as its string value.
/// </summary>
public sealed class CategoryIdConverter : StringIdConverter<CategoryId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public CategoryIdConverter()
		: base(value => new CategoryId(value))
	{
	}
	#endregion
}
