namespace KbcTracker.Domain.Features.Categories.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies a category: a readable slug of its first name (<c>restaurants-cafes</c>) for created
///              categories, a fixed word (<c>other</c>, <c>loan</c>…) for built-in ones. Stored in the user's
///              decisions, so it never changes once created.
/// </summary>
/// <param name="Value">The identifier value.</param>
public sealed record CategoryId(string Value) : StronglyTypedStringId<CategoryId>(Value)
{
	#region Static Instances
	/// <summary>
	/// The built-in fallback category, which cannot be deleted.
	/// </summary>
	public static readonly CategoryId Other = new("other");

	/// <summary>
	/// The built-in category of a loan payout: money in that is neither income nor spending.
	/// </summary>
	public static readonly CategoryId LoanReceived = new("loan-received");
	#endregion
}
