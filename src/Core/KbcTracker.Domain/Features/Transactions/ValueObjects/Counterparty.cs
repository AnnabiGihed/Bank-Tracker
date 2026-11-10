namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The other party of a transaction: who received the money out, or who sent the money in.
/// </summary>
public sealed record Counterparty
{
	#region Properties
	/// <summary>
	/// Gets the other party's name, trimmed.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the other party's IBAN without spaces, or <see langword="null"/> when the bank does not send it.
	/// </summary>
	public string? Iban { get; }
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="Counterparty"/>; use <see cref="Create"/> for untrusted input.
	/// </summary>
	private Counterparty(string name, string? iban)
	{
		Name = name;
		Iban = iban;
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates the other party of a transaction.
	/// </summary>
	/// <param name="name">The name; required.</param>
	/// <param name="iban">The IBAN, with or without spaces; optional.</param>
	/// <returns>The counterparty, or <see cref="TxnErrors.CounterpartyRequired"/> when the name is blank.</returns>
	public static Result<Counterparty> Create(string name, string? iban)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return Result.Failure<Counterparty>(TxnErrors.CounterpartyRequired);
		}

		var compactIban = string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", "", StringComparison.Ordinal);
		return new Counterparty(name.Trim(), compactIban);
	}

	/// <summary>
	/// Rebuilds a stored other party without the creation checks or normalisation: stored history is kept as it is.
	/// </summary>
	/// <param name="name">The stored name.</param>
	/// <param name="iban">The stored IBAN.</param>
	/// <returns>The counterparty.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	public static Counterparty Rehydrate(string name, string? iban)
	{
		ArgumentNullException.ThrowIfNull(name);
		return new Counterparty(name, iban);
	}
	#endregion
}
