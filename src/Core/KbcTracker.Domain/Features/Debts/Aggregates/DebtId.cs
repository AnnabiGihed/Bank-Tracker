namespace KbcTracker.Domain.Features.Debts.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies a debt the user tracks: ten hexadecimal characters, short enough to travel in the pages'
///              links and actions. Stored in the user's decisions, so it never changes.
/// </summary>
/// <param name="Value">The identifier value.</param>
public sealed record DebtId(string Value) : StronglyTypedStringId<DebtId>(Value)
{
	#region Factory Methods
	/// <summary>
	/// Creates a new random identifier.
	/// </summary>
	/// <returns>The identifier: the first ten characters of a new GUID.</returns>
	public static DebtId New() => new(Guid.NewGuid().ToString("N")[..10]);
	#endregion
}
