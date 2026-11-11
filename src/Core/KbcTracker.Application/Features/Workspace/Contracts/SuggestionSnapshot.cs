using KbcTracker.Domain.Features.FixedPayments.ValueObjects;
using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a pending fixed-payment suggestion with references to its safe transaction items.
/// </summary>
/// <param name="Signature">The existing suggestion identity.</param>
/// <param name="Name">The display name.</param>
/// <param name="CategoryId">The suggested category.</param>
/// <param name="Amount">The median amount.</param>
/// <param name="CadenceDays">The expected interval.</param>
/// <param name="HighConfidence">Whether the pattern is strong.</param>
/// <param name="Reason">The explanation shown by the UI.</param>
/// <param name="TransactionIds">The suggestion's transactions.</param>
/// <param name="SeriesKeys">The covered payee keys.</param>
/// <param name="Tokens">The matching terms.</param>
/// <param name="Kind">The movement kind.</param>
public sealed record SuggestionSnapshot(string Signature, string Name, string CategoryId, decimal Amount, int CadenceDays,
	bool HighConfidence, string Reason, IReadOnlyList<string> TransactionIds, IReadOnlyList<string> SeriesKeys, IReadOnlyList<string> Tokens, Kind Kind)
{
	#region Public Methods
	/// <summary>Copies a computed suggestion.</summary>
	/// <param name="suggestion">The central suggestion.</param>
	/// <returns>The snapshot.</returns>
	public static SuggestionSnapshot From(Suggestion suggestion) => new(suggestion.Signature, suggestion.Name, suggestion.CategoryId,
		suggestion.Amount, suggestion.CadenceDays, suggestion.HighConfidence, suggestion.Reason,
		[.. suggestion.Items.Select(i => i.T.Id.Value)], suggestion.SeriesKeys, suggestion.Tokens, suggestion.Kind);

	/// <summary>Restores a suggestion using the snapshot's shared transaction items.</summary>
	/// <param name="items">The classified items.</param>
	/// <returns>The display suggestion.</returns>
	public Suggestion ToDomain(IReadOnlyDictionary<string, TxnInfo> items) => new(Signature, Name, CategoryId, Amount,
		CadenceDays, HighConfidence, Reason, TransactionIds.Select(id => items[id]).ToArray(), SeriesKeys, Tokens, Kind);
	#endregion
}
