using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a string-valued strongly typed id as its value (<c>text</c>), so stored ids stay the bank's
///              and the user's readable ids. Each id type derives one converter with a parameterless constructor,
///              which EF Core conventions require.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public abstract class StringIdConverter<TId> : ValueConverter<TId, string>
	where TId : StronglyTypedStringId<TId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	/// <param name="create">Rebuilds the id from its stored value.</param>
	protected StringIdConverter(System.Linq.Expressions.Expression<Func<string, TId>> create)
		: base(id => id.Value, create)
	{
	}
	#endregion
}
