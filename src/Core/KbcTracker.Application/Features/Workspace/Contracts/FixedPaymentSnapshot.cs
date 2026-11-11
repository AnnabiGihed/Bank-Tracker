using KbcTracker.Domain.Features.FixedPayments.ValueObjects;
using KbcTracker.Domain.Features.FixedPayments.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers the transaction references belonging to an accepted fixed payment.
/// </summary>
/// <param name="Definition">The accepted definition.</param>
/// <param name="CategoryId">Its resolved display category.</param>
/// <param name="TransactionIds">The matched transactions, oldest first.</param>
public sealed record FixedPaymentSnapshot(RecurringPaymentSnapshot Definition, string CategoryId, IReadOnlyList<string> TransactionIds)
{
	#region Public Methods
	/// <summary>Copies a fixed-payment row with stable references.</summary>
	/// <param name="row">The computed row.</param>
	/// <returns>The snapshot.</returns>
	public static FixedPaymentSnapshot From(FixedRow row) => new(RecurringPaymentSnapshot.From(row.Def),
		row.Category.Id.Value, [.. row.Payments.Select(p => p.T.Id.Value)]);

	/// <summary>Restores the row with shared category, transaction and definition instances.</summary>
	/// <param name="categories">The snapshot categories.</param>
	/// <param name="items">The snapshot transactions.</param>
	/// <param name="recurring">The snapshot definitions.</param>
	/// <returns>The display row.</returns>
	public FixedRow ToDomain(IReadOnlyDictionary<string, Category> categories, IReadOnlyDictionary<string, TxnInfo> items,
		IReadOnlyDictionary<string, RecurringPayment> recurring) =>
		new(recurring[Definition.Id], categories[CategoryId], TransactionIds.Select(id => items[id]).ToArray());
	#endregion
}
