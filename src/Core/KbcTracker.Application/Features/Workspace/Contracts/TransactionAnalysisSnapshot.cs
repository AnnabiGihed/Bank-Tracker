using KbcTracker.Domain.Features.FixedPayments.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a classified transaction while keeping raw records, account ids and dedicated IBAN fields on the backend.
/// </summary>
/// <param name="Id">The permanent transaction identifier.</param>
/// <param name="Date">The booking date.</param>
/// <param name="Amount">The signed amount.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="Counterparty">The original display label.</param>
/// <param name="Description">The bank-provided free text.</param>
/// <param name="Kind">The centrally determined movement kind.</param>
/// <param name="SeriesKey">The opaque grouping key used by existing decisions.</param>
/// <param name="Name">The resolved display name.</param>
/// <param name="Tokens">The matching terms.</param>
/// <param name="CategoryId">The resolved category.</param>
/// <param name="CategoryConfirmed">Whether categorisation is confirmed.</param>
/// <param name="RecurringId">The matching fixed-payment identifier.</param>
/// <param name="Parts">The parts of a split payment, or <see langword="null"/> when it is not split.</param>
public sealed record TransactionAnalysisSnapshot(string Id, DateOnly Date, decimal Amount, string Currency, string Counterparty,
	string Description, Kind Kind, string SeriesKey, string Name, string[] Tokens, string CategoryId, bool CategoryConfirmed, string? RecurringId, IReadOnlyList<TxnPartSnapshot>? Parts = null)
{
	#region Public Methods
	/// <summary>Copies an already understood transaction, excluding its raw bank source.</summary>
	/// <param name="item">The central analysis item.</param>
	/// <returns>The client-safe snapshot.</returns>
	public static TransactionAnalysisSnapshot From(TxnInfo item) => new(item.T.Id.Value, item.T.Date, item.T.Amount,
		item.T.Currency, item.T.Counterparty, item.T.Description, item.Kind, item.SeriesKey, item.Name, [.. item.Tokens],
		item.Category.Id.Value, item.CategoryConfirmed, item.Recurring?.Id.Value,
		item.Split?.Select(TxnPartSnapshot.From).ToList());

	/// <summary>Rebuilds the classified item without rerunning classification on incomplete source data.</summary>
	/// <param name="categories">The snapshot definitions.</param>
	/// <param name="recurring">The snapshot fixed payments.</param>
	/// <returns>The display item.</returns>
	public TxnInfo ToDomain(IReadOnlyDictionary<string, Category> categories, IReadOnlyDictionary<string, RecurringPayment> recurring)
	{
		var transaction = Txn.Rehydrate(new TxnId(Id), "display-only", Date, Money.Rehydrate(Amount, Currency),
			KbcTracker.Domain.Features.Transactions.ValueObjects.Counterparty.Rehydrate(Counterparty, null), Description, BankRecord.None);
		return new(transaction, Kind, SeriesKey, Name, Tokens, categories[CategoryId], CategoryConfirmed,
			RecurringId is null ? null : recurring[RecurringId], Parts?.Select(p => p.ToDomain(categories)).ToList());
	}
	#endregion
}
