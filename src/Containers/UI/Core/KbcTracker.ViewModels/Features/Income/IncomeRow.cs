namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment of money in as the Income tab lists it: date, payer, amount and category, with the
///              requests of its confirm and category buttons, as a row of Transactions has them.
/// </summary>
/// <param name="Id">The transaction id.</param>
/// <param name="SeriesKey">The payer's series key.</param>
/// <param name="DateText">The booking date, as <c>dd/MM</c>.</param>
/// <param name="Payer">The payer's name.</param>
/// <param name="Description">The bank's description.</param>
/// <param name="Amount">The amount received, positive.</param>
/// <param name="AmountText">The amount, formatted.</param>
/// <param name="CategoryId">The payment's category.</param>
/// <param name="CategoryName">The name of that category.</param>
/// <param name="Confirmed">Whether its category is confirmed.</param>
public sealed record IncomeRow(string Id, string SeriesKey, string DateText, string Payer, string Description, decimal Amount, string AmountText,
	string CategoryId, string CategoryName, bool Confirmed)
{
	#region Properties
	/// <summary>Gets the request that confirms the automatic category of the payer.</summary>
	public DecisionRequest ConfirmRequest => DecisionRequests.Confirm(SeriesKey);
	#endregion

	#region Public Methods
	/// <summary>Builds the request that gives the payer another category, for all its payments past and future.</summary>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>The <c>payeeCategory</c> request.</returns>
	public DecisionRequest ChangeCategoryRequest(string categoryId) => DecisionRequests.PayeeCategory(SeriesKey, categoryId);

	/// <summary>Builds the request that undoes a change of the payer's category and gives this one payment the category instead.</summary>
	/// <param name="categoryId">The category the payer was just given.</param>
	/// <param name="previous">The payer's assignment from before the change, from the answer to <see cref="ChangeCategoryRequest"/>.</param>
	/// <returns>The <c>txnOnly</c> request.</returns>
	public DecisionRequest OnlyThisOneRequest(string categoryId, PayeeAssignmentSnapshot? previous) => DecisionRequests.TransactionOnly(SeriesKey, Id, categoryId, previous);
	#endregion
}
