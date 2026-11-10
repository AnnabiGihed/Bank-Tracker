namespace KbcTracker.Domain.Features.Shared.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies the user's decisions. The tracker has a single user, so there is a single instance,
///              <see cref="Current"/>.
/// </summary>
/// <param name="Value">The identifier value.</param>
public sealed record UserDataId(string Value) : StronglyTypedStringId<UserDataId>(Value)
{
	#region Static Instances
	/// <summary>
	/// The id of the one set of decisions the tracker keeps.
	/// </summary>
	public static readonly UserDataId Current = new("user");
	#endregion
}
