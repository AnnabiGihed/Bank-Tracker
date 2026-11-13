namespace KbcTracker.Components.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds the address of every tab, with the selected month in <c>?m=yyyy-MM</c>, so a link or a redirect
///              keeps the month the owner is looking at. The routes are the ones the old web pages used.
/// </summary>
public static class TabRoutes
{
	#region Constants
	/// <summary>The address of the bank picker.</summary>
	public const string Banks = "/banks";

	/// <summary>The address of the tab opened first.</summary>
	public const string Overview = "/overview";
	#endregion

	#region Public Methods
	/// <summary>Builds the address of a tab for a month.</summary>
	/// <param name="tabId">The id of the tab, such as <c>overview</c> or <c>fixed</c>.</param>
	/// <param name="month">The month as <c>yyyy-MM</c>, or <see langword="null"/> or empty for the newest.</param>
	/// <returns>The path with the month in its query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="tabId"/> is <see langword="null"/>.</exception>
	public static string Tab(string tabId, string? month)
	{
		ArgumentNullException.ThrowIfNull(tabId);
		return string.IsNullOrEmpty(month) ? "/" + tabId : $"/{tabId}?m={Uri.EscapeDataString(month)}";
	}

	/// <summary>Builds the address of the Transactions tab filtered on a category.</summary>
	/// <param name="month">The month as <c>yyyy-MM</c>.</param>
	/// <param name="categoryId">The category to filter on.</param>
	/// <returns>The path with the month and the category in its query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="month"/> or <paramref name="categoryId"/> is <see langword="null"/>.</exception>
	public static string TransactionsOf(string month, string categoryId)
	{
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(categoryId);
		return $"/transactions?m={Uri.EscapeDataString(month)}&cat={Uri.EscapeDataString(categoryId)}";
	}

	/// <summary>Builds the address of the Transactions tab limited to the payments behind a figure of the month.</summary>
	/// <param name="month">The month as <c>yyyy-MM</c>.</param>
	/// <param name="figure">The figure whose payments to show.</param>
	/// <returns>The path with the month and <c>show</c> in its query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="month"/> is <see langword="null"/>.</exception>
	public static string TransactionsBehind(string month, MonthFigure figure)
	{
		ArgumentNullException.ThrowIfNull(month);
		return $"/transactions?m={Uri.EscapeDataString(month)}&show={MonthFigureNames.ToQuery(figure)}";
	}
	#endregion
}
