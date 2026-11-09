namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The state of an accepted fixed payment in a given month, shown on the Fixed payments tab.
/// </summary>
public enum FixedStatus
{
	/// <summary>
	/// Paid this month, at the usual amount.
	/// </summary>
	Paid,

	/// <summary>
	/// Paid this month, but the amount differs from the usual one by at least 1 € and 2 %.
	/// </summary>
	PaidChanged,

	/// <summary>
	/// Not paid yet, and its expected date (plus 4 days) has not passed.
	/// </summary>
	Due,

	/// <summary>
	/// Not paid, and more than 4 days after its expected date.
	/// </summary>
	Late,

	/// <summary>
	/// Not expected this month (quarterly or yearly), or a past month without payment.
	/// </summary>
	NotThisMonth,

	/// <summary>
	/// The month is before its first payment.
	/// </summary>
	NotStarted,
}
