namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a <see cref="UserDataId"/> as its string value.
/// </summary>
public sealed class UserDataIdConverter : StringIdConverter<UserDataId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public UserDataIdConverter()
		: base(value => new UserDataId(value))
	{
	}
	#endregion
}
