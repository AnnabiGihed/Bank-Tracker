namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Where a debt stands today, the one word the Debts tab and the Overview notice show. The first state
///              that applies wins, in the order of this list.
/// </summary>
public enum DebtState
{
	/// <summary>
	/// More was repaid than the debt asks.
	/// </summary>
	Overpaid,

	/// <summary>
	/// Everything is repaid.
	/// </summary>
	Repaid,

	/// <summary>
	/// The owner has not said how the debt is repaid.
	/// </summary>
	NoPlan,

	/// <summary>
	/// A payment is overdue: its due date is before today and the repayments do not cover it.
	/// </summary>
	Overdue,

	/// <summary>
	/// The next payment is due within seven days, today included.
	/// </summary>
	DueSoon,

	/// <summary>
	/// Repayments are on schedule and nothing is due soon.
	/// </summary>
	OnTrack,
}
