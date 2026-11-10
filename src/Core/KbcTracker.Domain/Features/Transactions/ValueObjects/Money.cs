using System.Text.RegularExpressions;

namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : An amount of money in a currency, as a transaction carries it: negative for money out, positive for
///              money in. The currency is a three-letter ISO 4217 code.
/// </summary>
public readonly partial record struct Money
{
	#region Properties
	/// <summary>
	/// Gets the amount: negative for money out, positive for money in.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>
	/// Gets the ISO 4217 currency code, for example <c>EUR</c>.
	/// </summary>
	public string Currency { get; }
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="Money"/>; use <see cref="Create"/> for untrusted input.
	/// </summary>
	private Money(decimal amount, string currency)
	{
		Amount = amount;
		Currency = currency;
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates an amount of money.
	/// </summary>
	/// <param name="amount">The amount: negative for money out, positive for money in.</param>
	/// <param name="currency">The ISO 4217 currency code: three upper-case letters.</param>
	/// <returns>The money, or <see cref="TxnErrors.CurrencyInvalid"/> when the currency is not a three-letter code.</returns>
	public static Result<Money> Create(decimal amount, string currency) =>
		currency is not null && CurrencyCode().IsMatch(currency)
			? new Money(amount, currency)
			: Result.Failure<Money>(TxnErrors.CurrencyInvalid);

	/// <summary>
	/// Rebuilds stored money without the creation checks: stored history is kept as it is.
	/// </summary>
	/// <param name="amount">The stored amount.</param>
	/// <param name="currency">The stored currency code.</param>
	/// <returns>The money.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="currency"/> is <see langword="null"/>.</exception>
	public static Money Rehydrate(decimal amount, string currency)
	{
		ArgumentNullException.ThrowIfNull(currency);
		return new Money(amount, currency);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Matches a three-letter upper-case currency code.
	/// </summary>
	[GeneratedRegex("^[A-Z]{3}$", RegexOptions.None, 1000)]
	private static partial Regex CurrencyCode();
	#endregion
}
