namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment as the Transactions tab lists it: what the filters need to know about it, what its row
///              shows (name, category or split parts, amount, fixed-payment cell) and the requests its buttons send.
/// </summary>
/// <param name="Id">The transaction id.</param>
/// <param name="SeriesKey">The payee's series key.</param>
/// <param name="Name">The payee's name.</param>
/// <param name="Description">The bank's description.</param>
/// <param name="DateText">The booking date, as <c>dd/MM</c>.</param>
/// <param name="Amount">The amount; negative for money out.</param>
/// <param name="AmountText">The amount, formatted, with its sign.</param>
/// <param name="Type">The kind of payment, for the type filter.</param>
/// <param name="CategoryIds">The ids of the categories of its parts and of their ancestors, for the category filter.</param>
/// <param name="Confirmed">Whether its category is confirmed.</param>
/// <param name="SearchText">What the search looks in, in lower case.</param>
/// <param name="IsSplit">Whether it is split into parts.</param>
/// <param name="CategoryId">The category of the payment as a whole.</param>
/// <param name="CategoryName">The name of that category.</param>
/// <param name="Parts">The tags of its parts when it is split, otherwise empty.</param>
/// <param name="PartAmountsOut">For a split payment, what each category of its parts and their ancestors holds, as amounts out; otherwise empty.</param>
/// <param name="CanSplit">Whether it can be split: only spending can.</param>
/// <param name="Fixed">What its fixed-payment cell shows.</param>
/// <param name="FixedName">The name of the fixed payment it belongs to, or <see langword="null"/>.</param>
/// <param name="SplitSeed">The parts the split editor starts from when it is split already, otherwise <see langword="null"/>.</param>
/// <param name="SplitDefaultCategoryId">The category new parts start with: the payment's own when it is spending, otherwise empty.</param>
public sealed record TransactionRow(string Id, string SeriesKey, string Name, string Description, string DateText, decimal Amount, string AmountText,
	TransactionTypeFilter Type, IReadOnlyList<string> CategoryIds, bool Confirmed, string SearchText, bool IsSplit, string CategoryId, string CategoryName,
	IReadOnlyList<PartTag> Parts, IReadOnlyDictionary<string, decimal> PartAmountsOut, bool CanSplit, FixedMark Fixed, string? FixedName,
	IReadOnlyList<SplitPartSeed>? SplitSeed, string SplitDefaultCategoryId)
{
	#region Properties
	/// <summary>Gets a value indicating whether the amount is money in, which the tab shows in green.</summary>
	public bool IsIncome => Amount > 0;

	/// <summary>Gets the request that confirms the automatic category of the payee.</summary>
	public DecisionRequest ConfirmRequest => DecisionRequests.Confirm(SeriesKey);

	/// <summary>Gets the request that marks the payee as a fixed payment.</summary>
	public DecisionRequest MarkFixedRequest => DecisionRequests.Mark(SeriesKey);

	/// <summary>Gets the words that tell what the split button does.</summary>
	public string SplitButtonTitle => IsSplit ? "Edit the split" : "Split this payment into parts";
	#endregion

	#region Public Methods
	/// <summary>Builds the request that gives the payee another category, for all its payments past and future. Its answer
	/// carries the assignment from before, which <see cref="OnlyThisOneRequest"/> needs.</summary>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>The <c>payeeCategory</c> request.</returns>
	public DecisionRequest ChangeCategoryRequest(string categoryId) => DecisionRequests.PayeeCategory(SeriesKey, categoryId);

	/// <summary>Builds the request that undoes a change of the payee's category and gives this one payment the category instead.</summary>
	/// <param name="categoryId">The category the payee was just given.</param>
	/// <param name="previous">The payee's assignment from before the change, from the answer to <see cref="ChangeCategoryRequest"/>.</param>
	/// <returns>The <c>txnOnly</c> request.</returns>
	public DecisionRequest OnlyThisOneRequest(string categoryId, PayeeAssignmentSnapshot? previous) => DecisionRequests.TransactionOnly(SeriesKey, Id, categoryId, previous);
	#endregion
}
