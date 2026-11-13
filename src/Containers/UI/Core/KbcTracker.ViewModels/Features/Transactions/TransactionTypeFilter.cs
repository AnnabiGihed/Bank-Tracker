namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The kind of payment the Transactions tab shows; also the kind of each payment row.
/// </summary>
public enum TransactionTypeFilter
{
	/// <summary>Spending: money out that counts as spending.</summary>
	Spending,

	/// <summary>Income: money in.</summary>
	Income,

	/// <summary>Savings and investments.</summary>
	Savings,

	/// <summary>Own transfers between the owner's accounts.</summary>
	OwnTransfers,

	/// <summary>Every kind (a filter only; a row is never of this kind).</summary>
	Everything,
}
