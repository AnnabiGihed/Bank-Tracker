using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.FixedPayments.Aggregates;
using KbcTracker.Domain.Features.FixedPayments.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a fixed-payment definition without exposing bank credentials.
/// </summary>
/// <param name="Id">The stable definition identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="CategoryId">The chosen category, when any.</param>
/// <param name="SeriesKeys">The existing opaque payee identities.</param>
/// <param name="Tokens">The shop matching terms.</param>
/// <param name="Amount">The usual positive amount.</param>
/// <param name="TolerancePct">The matching tolerance in percent.</param>
/// <param name="CadenceDays">The recurrence interval.</param>
/// <param name="Active">Whether matching is enabled.</param>
/// <param name="CreatedAt">The acceptance time.</param>
public sealed record RecurringPaymentSnapshot(string Id, string Name, string? CategoryId, IReadOnlyList<string> SeriesKeys,
	IReadOnlyList<string> Tokens, decimal Amount, decimal TolerancePct, int CadenceDays, bool Active, DateTimeOffset CreatedAt)
{
	#region Public Methods
	/// <summary>Rebuilds a fixed-payment definition for display.</summary>
	/// <returns>The definition.</returns>
	public RecurringPayment ToDomain() => RecurringPayment.Rehydrate(new RecurringPaymentId(Id), Name,
		CategoryId is null ? null : new CategoryId(CategoryId), new PayeeMatch(SeriesKeys, Tokens),
		new RecurrenceRule(Amount, TolerancePct, CadenceDays), Active, CreatedAt);

	/// <summary>Copies a centrally stored definition.</summary>
	/// <param name="payment">The accepted definition.</param>
	/// <returns>The snapshot.</returns>
	public static RecurringPaymentSnapshot From(RecurringPayment payment) => new(payment.Id.Value, payment.Name,
		payment.CategoryId?.Value, [.. payment.SeriesKeys], [.. payment.Tokens], payment.Amount, payment.TolerancePct,
		payment.CadenceDays, payment.Active, payment.CreatedAt);
	#endregion
}
