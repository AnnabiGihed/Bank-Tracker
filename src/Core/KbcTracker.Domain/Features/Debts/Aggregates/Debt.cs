namespace KbcTracker.Domain.Features.Debts.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Money the owner borrowed and how it is repaid (ADR-0010): who lent it, what was received, the
///              repayment plan, and the payments the owner added or excluded by hand when the automatic matching
///              of <see cref="DebtLedger"/> is wrong.
///
///              An entity owned by the <see cref="UserData"/> aggregate, which adds, changes and removes it. It is
///              created only from a user action; the repayments found are never stored, only the corrections.
/// </summary>
public sealed class Debt : Entity<DebtId>
{
	#region Constants
	/// <summary>
	/// The most characters a lender's name has.
	/// </summary>
	public const int MaxLenderLength = 100;

	/// <summary>
	/// The most characters an IBAN has.
	/// </summary>
	public const int MaxIbanLength = 34;

	/// <summary>
	/// The start of the series key of a bank loan's instalments.
	/// </summary>
	public const string LoanSeriesPrefix = "LOAN:";
	#endregion

	#region Fields
	/// <summary>
	/// The payments the owner says are repayments.
	/// </summary>
	private readonly HashSet<TxnId> _included = [];

	/// <summary>
	/// The payments the owner says are no repayments.
	/// </summary>
	private readonly HashSet<TxnId> _excluded = [];
	#endregion

	#region Properties
	/// <summary>
	/// Gets whether the money came from a person or a bank.
	/// </summary>
	public DebtKind Kind { get; private set; }

	/// <summary>
	/// Gets who lent the money.
	/// </summary>
	public string Lender { get; private set; }

	/// <summary>
	/// Gets the lender's IBAN, or <see langword="null"/>.
	/// </summary>
	public string? Iban { get; private set; }

	/// <summary>
	/// Gets the credit that paid the money out, or <see langword="null"/> when the loan is older than the bank's history.
	/// </summary>
	public TxnId? ReceivedPaymentId { get; private set; }

	/// <summary>
	/// Gets the amount received.
	/// </summary>
	public decimal Principal { get; private set; }

	/// <summary>
	/// Gets the <c>LOAN:</c> series of a bank loan's instalments, or <see langword="null"/>.
	/// </summary>
	public string? SeriesKey { get; private set; }

	/// <summary>
	/// Gets the day the money was received; payments before it never repay the debt.
	/// </summary>
	public DateOnly BorrowedOn { get; private set; }

	/// <summary>
	/// Gets how the debt is repaid, or <see langword="null"/> while the owner has not said.
	/// </summary>
	public RepaymentPlan? Plan { get; private set; }

	/// <summary>
	/// Gets the payments the owner added as repayments; they always count.
	/// </summary>
	public IReadOnlySet<TxnId> IncludedPayments => _included;

	/// <summary>
	/// Gets the payments the owner excluded; they never count.
	/// </summary>
	public IReadOnlySet<TxnId> ExcludedPayments => _excluded;

	/// <summary>
	/// Gets the amount the repayments must reach: the plan's total, or the amount received while there is no plan.
	/// </summary>
	public decimal AmountToRepay => Plan?.Amount ?? Principal;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a debt from terms that are already valid or stored.
	/// </summary>
	private Debt(DebtId id, DebtTerms terms)
		: base(id)
	{
		Lender = terms.Lender;
		Apply(terms);
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates a debt, checking and normalising what the owner said.
	/// </summary>
	/// <param name="id">The new debt's identifier.</param>
	/// <param name="terms">The terms; the lender is trimmed, the IBAN has no spaces and is upper case.</param>
	/// <returns>The debt, or <see cref="DebtErrors.LenderInvalid"/>, <see cref="DebtErrors.PrincipalInvalid"/>,
	/// <see cref="DebtErrors.IbanInvalid"/>, <see cref="DebtErrors.BorrowedOnInvalid"/> or <see cref="DebtErrors.SeriesKeyInvalid"/>.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static Result<Debt> Create(DebtId id, DebtTerms terms)
	{
		ArgumentNullException.ThrowIfNull(id);
		var checkedTerms = Check(terms);
		return checkedTerms.IsFailure
			? Result.Failure<Debt>(checkedTerms.Error, checkedTerms.ResultExceptionType)
			: new Debt(id, checkedTerms.Value);
	}

	/// <summary>
	/// Rebuilds a stored debt without the checks: stored decisions are kept as they are.
	/// </summary>
	/// <param name="id">The debt's identifier.</param>
	/// <param name="terms">The stored terms.</param>
	/// <param name="included">The payments added by hand.</param>
	/// <param name="excluded">The payments excluded by hand.</param>
	/// <returns>The debt.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static Debt Rehydrate(DebtId id, DebtTerms terms, IEnumerable<TxnId> included, IEnumerable<TxnId> excluded)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(terms);
		ArgumentNullException.ThrowIfNull(included);
		ArgumentNullException.ThrowIfNull(excluded);
		var debt = new Debt(id, terms);
		debt._included.UnionWith(included);
		debt._excluded.UnionWith(excluded);
		return debt;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Replaces what the owner said about the debt, keeping the payments added or excluded by hand.
	/// </summary>
	/// <param name="terms">The new terms, checked like those of <see cref="Create"/>.</param>
	/// <returns>Success, or the error that rejects the terms; the debt is then unchanged.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="terms"/> is <see langword="null"/>.</exception>
	public Result Revise(DebtTerms terms)
	{
		var checkedTerms = Check(terms);
		if (checkedTerms.IsFailure)
		{
			return Result.Failure(checkedTerms.Error, checkedTerms.ResultExceptionType);
		}

		Lender = checkedTerms.Value.Lender;
		Apply(checkedTerms.Value);
		return Result.Success();
	}

	/// <summary>
	/// Says that a payment is a repayment of this debt; an earlier exclusion is forgotten.
	/// </summary>
	/// <param name="paymentId">The payment.</param>
	/// <exception cref="ArgumentNullException"><paramref name="paymentId"/> is <see langword="null"/>.</exception>
	public void Include(TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(paymentId);
		_excluded.Remove(paymentId);
		_included.Add(paymentId);
	}

	/// <summary>
	/// Says that a payment is no repayment of this debt; an earlier inclusion is forgotten.
	/// </summary>
	/// <param name="paymentId">The payment.</param>
	/// <exception cref="ArgumentNullException"><paramref name="paymentId"/> is <see langword="null"/>.</exception>
	public void Exclude(TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(paymentId);
		_included.Remove(paymentId);
		_excluded.Add(paymentId);
	}

	/// <summary>
	/// Forgets what the owner said about a payment, so the automatic matching decides again.
	/// </summary>
	/// <param name="paymentId">The payment.</param>
	/// <exception cref="ArgumentNullException"><paramref name="paymentId"/> is <see langword="null"/>.</exception>
	public void Reset(TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(paymentId);
		_included.Remove(paymentId);
		_excluded.Remove(paymentId);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Copies the terms that change after creation; the lender is set by the caller because it is not nullable.
	/// </summary>
	private void Apply(DebtTerms terms)
	{
		Kind = terms.Kind;
		Iban = terms.Iban;
		ReceivedPaymentId = terms.ReceivedPaymentId;
		Principal = terms.Principal;
		SeriesKey = terms.SeriesKey;
		Plan = terms.Plan;
		BorrowedOn = terms.BorrowedOn;
	}

	/// <summary>
	/// Checks and normalises the terms.
	/// </summary>
	private static Result<DebtTerms> Check(DebtTerms terms)
	{
		ArgumentNullException.ThrowIfNull(terms);
		var lender = terms.Lender.Trim();
		if (lender.Length is 0 or > MaxLenderLength)
		{
			return Result.Failure<DebtTerms>(DebtErrors.LenderInvalid);
		}

		if (terms.Principal <= 0 || decimal.Round(terms.Principal, 2) != terms.Principal)
		{
			return Result.Failure<DebtTerms>(DebtErrors.PrincipalInvalid);
		}

		if (terms.BorrowedOn.Year is < RepaymentPlan.MinYear or > RepaymentPlan.MaxYear)
		{
			return Result.Failure<DebtTerms>(DebtErrors.BorrowedOnInvalid);
		}

		var iban = string.IsNullOrWhiteSpace(terms.Iban) ? null : terms.Iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
		if (iban is not null && (iban.Length > MaxIbanLength || !iban.All(char.IsAsciiLetterOrDigit)))
		{
			return Result.Failure<DebtTerms>(DebtErrors.IbanInvalid);
		}

		var seriesKey = string.IsNullOrWhiteSpace(terms.SeriesKey) ? null : terms.SeriesKey.Trim();
		if (seriesKey is not null && !seriesKey.StartsWith(LoanSeriesPrefix, StringComparison.Ordinal))
		{
			return Result.Failure<DebtTerms>(DebtErrors.SeriesKeyInvalid);
		}

		return terms with { Lender = lender, Iban = iban, SeriesKey = seriesKey };
	}
	#endregion
}
