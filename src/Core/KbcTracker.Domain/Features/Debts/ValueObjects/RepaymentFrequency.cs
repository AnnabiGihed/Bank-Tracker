namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How often an instalment of a repayment plan falls due.
/// </summary>
public enum RepaymentFrequency
{
	/// <summary>
	/// Every seven days.
	/// </summary>
	Weekly,

	/// <summary>
	/// Every month, on the plan's day of the month.
	/// </summary>
	Monthly,

	/// <summary>
	/// Every three months, on the plan's day of the month.
	/// </summary>
	Quarterly,

	/// <summary>
	/// Every twelve months, on the plan's day of the month.
	/// </summary>
	Yearly,
}
