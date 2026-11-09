namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A possible fixed payment found in the history, waiting for the user to accept or dismiss it.
///              Suggestions are never accepted automatically.
/// </summary>
/// <param name="Signature">The identity of the suggestion: its series keys, sorted and joined with <c>|</c>; stored when dismissed.</param>
/// <param name="Name">The most frequent payee name of its payments.</param>
/// <param name="CategoryId">The most frequent category of its payments.</param>
/// <param name="Amount">The median amount, as a positive number.</param>
/// <param name="CadenceDays">The rhythm in days: 30, 91 or 365.</param>
/// <param name="HighConfidence">Whether the bank code says it is a contract, or it was seen at least three times.</param>
/// <param name="Reason">Why it was suggested, shown to the user.</param>
/// <param name="Items">Its payments, oldest first.</param>
/// <param name="SeriesKeys">The payee series it groups.</param>
/// <param name="Tokens">For card payments, the shop words common to all its payments; empty otherwise.</param>
/// <param name="Kind">The most frequent kind of its payments.</param>
public sealed record Suggestion(string Signature, string Name, string CategoryId, decimal Amount, int CadenceDays,
	bool HighConfidence, string Reason, IReadOnlyList<TxnInfo> Items, IReadOnlyList<string> SeriesKeys, IReadOnlyList<string> Tokens, Kind Kind)
{
	#region Properties
	/// <summary>
	/// Gets the rhythm as a word: Monthly, Quarterly or Yearly.
	/// </summary>
	public string Cadence => Analysis.CadenceName(CadenceDays);
	#endregion
}
