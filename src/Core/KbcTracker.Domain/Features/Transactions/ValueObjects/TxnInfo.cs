namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A transaction as the analysis understands it: its kind, payee series, display name, category and
///              the fixed payment it belongs to. Built by <see cref="Analysis"/>; never stored.
/// </summary>
/// <param name="T">The stored transaction.</param>
/// <param name="Kind">What kind of movement it is.</param>
/// <param name="SeriesKey">The payee series it belongs to, for example <c>CARD:GOOGLE YOUTUBE</c> or <c>DD:&lt;creditor&gt;:&lt;mandate&gt;</c>.</param>
/// <param name="Name">The payee or shop name shown to the user.</param>
/// <param name="Tokens">The distinctive words of <paramref name="Name"/>, used to recognise the payee when the bank label changes.</param>
/// <param name="Category">The resolved category.</param>
/// <param name="CategoryConfirmed">Whether the user chose or confirmed the category, or the bank code makes it certain.</param>
/// <param name="Recurring">The accepted fixed payment it was matched to, if any.</param>
public sealed record TxnInfo(Txn T, Kind Kind, string SeriesKey, string Name, string[] Tokens, Category Category, bool CategoryConfirmed, RecurringPayment? Recurring)
{
	#region Properties
	/// <summary>
	/// Gets a value indicating whether it is money out counted as spending.
	/// </summary>
	public bool IsSpending => Category.Type == CategoryType.Expense && T.Amount < 0;

	/// <summary>
	/// Gets a value indicating whether it is money in counted as income.
	/// </summary>
	public bool IsIncome => Category.Type == CategoryType.Income && T.Amount > 0;

	/// <summary>
	/// Gets a value indicating whether it is money put aside.
	/// </summary>
	public bool IsSaving => Category.Type == CategoryType.Savings;

	/// <summary>
	/// Gets a value indicating whether it moves money between the user's own accounts.
	/// </summary>
	public bool IsOwnMove => Category.Type == CategoryType.Transfer;

	/// <summary>
	/// Gets the month of the transaction as <c>yyyy-MM</c>.
	/// </summary>
	public string Month => Analysis.MonthOf(T.Date);
	#endregion
}
