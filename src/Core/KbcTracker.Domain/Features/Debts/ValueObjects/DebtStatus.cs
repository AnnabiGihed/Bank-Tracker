namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Where one debt stands on a given day: what was repaid, what remains, what is due next, what is late and
///              the schedule with the repayments laid over it. Built by <see cref="DebtLedger"/>; never stored.
/// </summary>
/// <param name="Debt">The debt.</param>
/// <param name="State">The one word that sums its situation up.</param>
/// <param name="Paid">The sum of the repayments found.</param>
/// <param name="Remaining">What is left to repay, never below zero.</param>
/// <param name="Overpaid">What was repaid beyond the amount to repay.</param>
/// <param name="NextDueDate">The due date of the next payment not yet covered, today included, or
/// <see langword="null"/> when there is none.</param>
/// <param name="NextDueAmount">What that payment still asks, or <see langword="null"/> when there is none.</param>
/// <param name="OverdueAmount">What payments due before today and not covered still ask.</param>
/// <param name="OldestMissedDate">The due date of the oldest of them, or <see langword="null"/> when none is overdue.</param>
/// <param name="Schedule">The plan's payments with what each is covered; empty without a plan.</param>
/// <param name="Payments">The repayments found, oldest first.</param>
public sealed record DebtStatus(Debt Debt, DebtState State, decimal Paid, decimal Remaining, decimal Overpaid, DateOnly? NextDueDate,
	decimal? NextDueAmount, decimal OverdueAmount, DateOnly? OldestMissedDate, IReadOnlyList<ScheduleRow> Schedule, IReadOnlyList<TxnInfo> Payments)
{
	#region Properties
	/// <summary>
	/// Gets the share of the amount to repay that was repaid, from 0 to 1.
	/// </summary>
	public decimal Progress => Debt.AmountToRepay <= 0 ? 0 : Math.Min(1m, Paid / Debt.AmountToRepay);
	#endregion
}
