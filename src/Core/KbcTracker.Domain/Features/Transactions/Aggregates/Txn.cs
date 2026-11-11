namespace KbcTracker.Domain.Features.Transactions.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One bank transaction of the permanent history: an aggregate root that never changes once
///              downloaded and is never deleted, because the bank only returns about 90 days.
///
///              A lightweight aggregate root (no audit, no soft delete): the bank is the source of truth for its
///              content. The original JSON is kept in <see cref="Raw"/> because the analysis reads fields from it
///              that have no property here.
/// </summary>
public sealed class Txn : LightweightAggregateRoot<TxnId>
{
	#region Properties
	/// <summary>
	/// Gets the Enable Banking account the transaction belongs to.
	/// </summary>
	public string AccountUid { get; private set; }

	/// <summary>
	/// Gets the booking date (or value or transaction date when the bank sends no booking date).
	/// </summary>
	public DateOnly Date { get; private set; }

	/// <summary>
	/// Gets the amount: negative for money out, positive for money in.
	/// </summary>
	public decimal Amount { get; private set; }

	/// <summary>
	/// Gets the ISO 4217 currency code, usually <c>EUR</c>.
	/// </summary>
	public string Currency { get; private set; }

	/// <summary>
	/// Gets the other party's name, or the start of the description when the bank sends no name.
	/// </summary>
	public string Counterparty { get; private set; }

	/// <summary>
	/// Gets the remittance information joined with the note, additional information and reference.
	/// </summary>
	public string Description { get; private set; }

	/// <summary>
	/// Gets the other party's IBAN without spaces, when known.
	/// </summary>
	public string? CounterpartyIban { get; private set; }

	/// <summary>
	/// Gets the domain of the ISO bank transaction code (for example <c>PMNT</c>), when sent.
	/// </summary>
	public string? BankCode { get; private set; }

	/// <summary>
	/// Gets the original JSON from the bank.
	/// </summary>
	public string? Raw { get; private set; }
	#endregion

	#region Constructors
	/// <summary>
	/// Constructor used by EF Core to materialise a stored transaction; the persistence layer then sets its values. Domain
	/// code creates instances only through the factory methods. It is <see langword="internal"/> rather than private
	/// because the analyzers report an unused private constructor (S1144) without seeing EF Core's reflection.
	/// </summary>
	internal Txn()
	{
		AccountUid = "";
		Currency = "";
		Counterparty = "";
		Description = "";
	}

	/// <summary>
	/// Initialises a new <see cref="Txn"/> from already valid parts.
	/// </summary>
	private Txn(TxnId id, string accountUid, DateOnly date, Money money, Counterparty counterparty, string description, BankRecord source)
		: base(id)
	{
		AccountUid = accountUid;
		Date = date;
		Amount = money.Amount;
		Currency = money.Currency;
		Counterparty = counterparty.Name;
		CounterpartyIban = counterparty.Iban;
		Description = description;
		BankCode = source.BankCode;
		Raw = source.Raw;
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates a transaction downloaded from the bank.
	/// </summary>
	/// <param name="id">The transaction's identifier.</param>
	/// <param name="accountUid">The Enable Banking account it belongs to; required.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="money">The amount and currency.</param>
	/// <param name="counterparty">The other party.</param>
	/// <param name="description">The description; may be empty, not <see langword="null"/>.</param>
	/// <param name="source">The bank's code and original JSON.</param>
	/// <returns>The transaction, or <see cref="TxnErrors.AccountRequired"/> when the account is blank.</returns>
	/// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
	public static Result<Txn> Create(TxnId id, string accountUid, DateOnly date, Money money, Counterparty counterparty, string description, BankRecord source)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(counterparty);
		ArgumentNullException.ThrowIfNull(description);
		ArgumentNullException.ThrowIfNull(source);
		if (string.IsNullOrWhiteSpace(accountUid))
		{
			return Result.Failure<Txn>(TxnErrors.AccountRequired);
		}

		return new Txn(id, accountUid, date, money, counterparty, description, source);
	}

	/// <summary>
	/// Rebuilds a transaction that was already stored, without the creation checks: stored history is kept as it
	/// is, even when written by an older version. Pair it with <see cref="Money.Rehydrate"/> and
	/// <see cref="ValueObjects.Counterparty.Rehydrate"/>.
	/// </summary>
	/// <param name="id">The transaction's identifier.</param>
	/// <param name="accountUid">The Enable Banking account.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="money">The stored amount and currency.</param>
	/// <param name="counterparty">The stored other party.</param>
	/// <param name="description">The stored description.</param>
	/// <param name="source">The stored bank code and JSON.</param>
	/// <returns>The transaction.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static Txn Rehydrate(TxnId id, string accountUid, DateOnly date, Money money, Counterparty counterparty, string description, BankRecord source)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(accountUid);
		ArgumentNullException.ThrowIfNull(counterparty);
		ArgumentNullException.ThrowIfNull(description);
		ArgumentNullException.ThrowIfNull(source);
		return new Txn(id, accountUid, date, money, counterparty, description, source);
	}
	#endregion
}
