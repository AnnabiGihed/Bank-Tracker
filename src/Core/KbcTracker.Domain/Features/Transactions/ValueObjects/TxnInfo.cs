namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A transaction as the analysis understands it: its kind, payee series, display name, category and
///              the fixed payment it belongs to, and the parts its money is divided into. Built by
///              <see cref="Analysis"/>; never stored.
/// </summary>
/// <param name="T">The stored transaction.</param>
/// <param name="Kind">What kind of movement it is.</param>
/// <param name="SeriesKey">The payee series it belongs to, for example <c>CARD:GOOGLE YOUTUBE</c> or <c>DD:&lt;creditor&gt;:&lt;mandate&gt;</c>.</param>
/// <param name="Name">The payee or shop name shown to the user.</param>
/// <param name="Tokens">The distinctive words of <paramref name="Name"/>, used to recognise the payee when the bank label changes.</param>
/// <param name="Category">The resolved category, without any split: payee rules and suggestions learn from this one.</param>
/// <param name="CategoryConfirmed">Whether the user chose or confirmed the category, or the bank code makes it certain.</param>
/// <param name="Recurring">The accepted fixed payment it was matched to, if any.</param>
/// <param name="Split">The parts the user divided the payment into, or <see langword="null"/> when it is not split.</param>
public sealed record TxnInfo(Txn T, Kind Kind, string SeriesKey, string Name, string[] Tokens, Category Category, bool CategoryConfirmed, RecurringPayment? Recurring, IReadOnlyList<TxnPart>? Split = null)
{
	#region Properties
	/// <summary>
	/// Gets a value indicating whether the user divided the payment into parts.
	/// </summary>
	public bool IsSplit => Split is not null;

	/// <summary>
	/// Gets the parts the money of the payment belongs to: the split, or one part with the whole amount and the
	/// payment's category.
	/// </summary>
	public IReadOnlyList<TxnPart> Parts => Split ?? [new TxnPart(Category, T.Amount, null)];

	/// <summary>
	/// Gets a value indicating whether it is money out counted as spending.
	/// </summary>
	public bool IsSpending => EffectiveType == CategoryType.Expense && T.Amount < 0;

	/// <summary>
	/// Gets a value indicating whether it is money in counted as income.
	/// </summary>
	public bool IsIncome => EffectiveType == CategoryType.Income && T.Amount > 0;

	/// <summary>
	/// Gets a value indicating whether it is money put aside.
	/// </summary>
	public bool IsSaving => EffectiveType == CategoryType.Savings;

	/// <summary>
	/// Gets a value indicating whether it moves money between the user's own accounts.
	/// </summary>
	public bool IsOwnMove => EffectiveType == CategoryType.Transfer;

	/// <summary>
	/// Gets the month of the transaction as <c>yyyy-MM</c>.
	/// </summary>
	public string Month => Analysis.MonthOf(T.Date);

	/// <summary>
	/// Gets the type that decides the totals: a split payment is spending whatever its own category is, because every
	/// part is.
	/// </summary>
	public CategoryType EffectiveType => IsSplit ? CategoryType.Expense : Category.Type;
	#endregion
}
