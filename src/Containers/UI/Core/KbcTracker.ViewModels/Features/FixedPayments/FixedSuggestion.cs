namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One suggested fixed payment for the owner to accept or dismiss, with the payments it was found from.
/// </summary>
/// <param name="Signature">The suggestion's signature, the key of accepting or dismissing it.</param>
/// <param name="Name">The payee's name.</param>
/// <param name="IsMaybe">Whether only two payments were seen so far.</param>
/// <param name="AmountText">The amount, formatted.</param>
/// <param name="Rhythm">Monthly, quarterly or yearly, in lower case.</param>
/// <param name="Reason">Why it was suggested.</param>
/// <param name="CategoryName">The category it would get.</param>
/// <param name="CategoryColor">The category's colour.</param>
/// <param name="Chips">The last payments it was found from, oldest first.</param>
public sealed record FixedSuggestion(string Signature, string Name, bool IsMaybe, string AmountText, string Rhythm, string Reason, string CategoryName,
	string CategoryColor, IReadOnlyList<SuggestionChip> Chips)
{
	#region Properties
	/// <summary>Gets the request that accepts the suggestion.</summary>
	public DecisionRequest AcceptRequest => DecisionRequests.Accept(Signature);

	/// <summary>Gets the request that dismisses the suggestion.</summary>
	public DecisionRequest DismissRequest => DecisionRequests.Dismiss(Signature);
	#endregion
}
