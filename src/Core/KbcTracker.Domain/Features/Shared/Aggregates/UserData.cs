namespace KbcTracker.Domain.Features.Shared.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Everything the user decided in the UI: accepted fixed payments, dismissed suggestions, categories
///              per payee and per transaction, and payee names.
///
///              Kept apart from the transactions so a re-sync never overwrites a decision. Keys are compared
///              ordinally, as they are ids built by the analysis.
/// </summary>
public sealed class UserData
{
	#region Properties
	/// <summary>
	/// Gets or sets the accepted fixed payments.
	/// </summary>
	public IList<RecurringPayment> Recurring { get; set; } = [];

	/// <summary>
	/// Gets or sets the suggestions the user rejected, by signature (their series keys joined with <c>|</c>).
	/// </summary>
	public ISet<string> DismissedSuggestions { get; set; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the category per payee series key; it applies to all transactions of that payee.
	/// </summary>
	public IDictionary<string, PayeeAssignment> Payees { get; set; } = new Dictionary<string, PayeeAssignment>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the category of single transactions by transaction id; it overrides the payee's category.
	/// </summary>
	public IDictionary<string, string> TxnCategories { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the names the user gave to payees, by series key.
	/// </summary>
	public IDictionary<string, string> PayeeNames { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
	#endregion
}
