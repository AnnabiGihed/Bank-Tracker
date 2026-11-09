namespace KbcTracker.Domain.Features.Categories.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How the transactions of a category count in the monthly totals. Stored by name, so members are
///              never renamed.
/// </summary>
public enum CategoryType
{
	/// <summary>
	/// Counted as spending (money out; a refund in such a category reduces spending).
	/// </summary>
	Expense,

	/// <summary>
	/// Counted as income.
	/// </summary>
	Income,

	/// <summary>
	/// Money put aside: shown separately, not counted as spending.
	/// </summary>
	Savings,

	/// <summary>
	/// A move between the user's own accounts: left out of every total.
	/// </summary>
	Transfer,
}
