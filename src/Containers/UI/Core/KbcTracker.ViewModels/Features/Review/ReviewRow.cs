namespace KbcTracker.ViewModels.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payee whose automatic category the owner has not confirmed yet, as the To review tab lists it.
/// </summary>
/// <param name="SeriesKey">The payee's series key, the key of every decision about it.</param>
/// <param name="Name">The payee's name.</param>
/// <param name="Sample">A sample of the bank's description of its payments.</param>
/// <param name="Count">The number of payments.</param>
/// <param name="Total">The total of its payments; negative for money out.</param>
/// <param name="TotalText">The total, formatted.</param>
/// <param name="LastText">The date of the last payment, as <c>dd/MM/yy</c>.</param>
/// <param name="Incoming">Whether the money comes in.</param>
/// <param name="CategoryId">The automatic category's id.</param>
/// <param name="CategoryName">The automatic category's name.</param>
public sealed record ReviewRow(string SeriesKey, string Name, string Sample, int Count, decimal Total, string TotalText, string LastText, bool Incoming,
	string CategoryId, string CategoryName)
{
	#region Properties
	/// <summary>Gets a value indicating whether the total is money in, which the tab shows in green.</summary>
	public bool TotalIsPositive => Total > 0;

	/// <summary>Gets the request that confirms the automatic category.</summary>
	public DecisionRequest ConfirmRequest => DecisionRequests.Confirm(SeriesKey);
	#endregion

	#region Public Methods
	/// <summary>Builds the request that gives the payee another category, for all its payments past and future.</summary>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>The <c>payeeCategory</c> request.</returns>
	public DecisionRequest ChangeCategoryRequest(string categoryId) => DecisionRequests.PayeeCategory(SeriesKey, categoryId);
	#endregion
}
