using KbcTracker.Domain.Features.Debts.Aggregates;
using KbcTracker.Domain.Features.Debts.ValueObjects;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a debt and where it stands (ADR-0010): what the owner said, the repayments found, the schedule
///              laid over with what each line is covered, and what is due or late. The payments travel as ids of the
///              transactions in the same snapshot.
/// </summary>
/// <param name="Id">The debt's identifier.</param>
/// <param name="Kind">Whether the money came from a person or a bank.</param>
/// <param name="Lender">Who lent it.</param>
/// <param name="Iban">The lender's IBAN, or <see langword="null"/>.</param>
/// <param name="ReceivedPaymentId">The credit that paid the money out, or <see langword="null"/>.</param>
/// <param name="Principal">The amount received.</param>
/// <param name="SeriesKey">The <c>LOAN:</c> series of a bank loan, or <see langword="null"/>.</param>
/// <param name="BorrowedOn">The day the money was received.</param>
/// <param name="Plan">How it is repaid, or <see langword="null"/> while there is no plan.</param>
/// <param name="IncludedPaymentIds">The payments the owner added as repayments.</param>
/// <param name="ExcludedPaymentIds">The payments the owner excluded.</param>
/// <param name="State">The one word that sums up the debt today.</param>
/// <param name="Paid">The sum of the repayments found.</param>
/// <param name="Remaining">What is left to repay.</param>
/// <param name="Overpaid">What was repaid beyond the amount to repay.</param>
/// <param name="NextDueDate">The due date of the next payment not yet covered, or <see langword="null"/>.</param>
/// <param name="NextDueAmount">What that payment still asks, or <see langword="null"/>.</param>
/// <param name="OverdueAmount">What payments due before today and not covered still ask.</param>
/// <param name="OldestMissedDate">The due date of the oldest of them, or <see langword="null"/>.</param>
/// <param name="Schedule">The plan's payments with what each is covered; empty without a plan.</param>
/// <param name="PaymentIds">The repayments found, oldest first.</param>
public sealed record DebtSnapshot(string Id, DebtKind Kind, string Lender, string? Iban, string? ReceivedPaymentId, decimal Principal, string? SeriesKey,
	DateOnly BorrowedOn, RepaymentPlanSnapshot? Plan, IReadOnlyList<string> IncludedPaymentIds, IReadOnlyList<string> ExcludedPaymentIds, DebtState State, decimal Paid,
	decimal Remaining, decimal Overpaid, DateOnly? NextDueDate, decimal? NextDueAmount, decimal OverdueAmount, DateOnly? OldestMissedDate,
	IReadOnlyList<ScheduleRow> Schedule, IReadOnlyList<string> PaymentIds)
{
	#region Public Methods
	/// <summary>Copies a debt's status.</summary>
	/// <param name="status">The computed status.</param>
	/// <returns>The snapshot.</returns>
	public static DebtSnapshot From(DebtStatus status)
	{
		ArgumentNullException.ThrowIfNull(status);
		var debt = status.Debt;
		return new(debt.Id.Value, debt.Kind, debt.Lender, debt.Iban, debt.ReceivedPaymentId?.Value, debt.Principal, debt.SeriesKey, debt.BorrowedOn,
			debt.Plan is null ? null : RepaymentPlanSnapshot.From(debt.Plan),
			[.. debt.IncludedPayments.Select(p => p.Value).Order(StringComparer.Ordinal)], [.. debt.ExcludedPayments.Select(p => p.Value).Order(StringComparer.Ordinal)],
			status.State, status.Paid, status.Remaining, status.Overpaid, status.NextDueDate, status.NextDueAmount, status.OverdueAmount,
			status.OldestMissedDate, [.. status.Schedule], [.. status.Payments.Select(p => p.T.Id.Value)]);
	}

	/// <summary>Rebuilds the status with the transactions of the same snapshot.</summary>
	/// <param name="items">The snapshot transactions by id.</param>
	/// <returns>The display status.</returns>
	public DebtStatus ToDomain(IReadOnlyDictionary<string, TxnInfo> items)
	{
		var terms = new DebtTerms(Kind, Lender, Iban, ReceivedPaymentId is null ? null : new TxnId(ReceivedPaymentId), Principal, SeriesKey, Plan?.ToDomain(), BorrowedOn);
		var debt = Debt.Rehydrate(new DebtId(Id), terms, IncludedPaymentIds.Select(p => new TxnId(p)), ExcludedPaymentIds.Select(p => new TxnId(p)));
		return new DebtStatus(debt, State, Paid, Remaining, Overpaid, NextDueDate, NextDueAmount, OverdueAmount, OldestMissedDate, Schedule,
			[.. PaymentIds.Select(id => items[id])]);
	}
	#endregion
}
