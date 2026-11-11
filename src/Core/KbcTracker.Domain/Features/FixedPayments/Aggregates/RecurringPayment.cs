namespace KbcTracker.Domain.Features.FixedPayments.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A fixed payment the user accepted. New transactions are matched to it automatically, by payee
///              series or by shop words and amount, so a changed bank label does not break it.
///
///              An entity owned by the <see cref="UserData"/> aggregate, which adds, changes and removes it: it is
///              created only from a user action (accepting a suggestion or marking a payee), never automatically.
/// </summary>
public sealed class RecurringPayment : Entity<RecurringPaymentId>
{
	#region Fields
	/// <summary>
	/// The payee series it covers.
	/// </summary>
	private readonly List<string> _seriesKeys = [];

	/// <summary>
	/// The shop words that identify it.
	/// </summary>
	private readonly List<string> _tokens = [];
	#endregion

	#region Properties
	/// <summary>
	/// Gets the name shown to the user.
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets the category of its payments, or <see langword="null"/> to use the payments' own category.
	/// </summary>
	public CategoryId? CategoryId { get; private set; }

	/// <summary>
	/// Gets the payee series it covers (direct-debit mandate, IBAN, shop…).
	/// </summary>
	public IReadOnlyList<string> SeriesKeys => _seriesKeys.AsReadOnly();

	/// <summary>
	/// Gets shop-name words that identify it even if the bank label changes (for example GOOGLE YOUTUBE).
	/// </summary>
	public IReadOnlyList<string> Tokens => _tokens.AsReadOnly();

	/// <summary>
	/// Gets the usual amount, as a positive number.
	/// </summary>
	public decimal Amount { get; private set; }

	/// <summary>
	/// Gets the allowed difference with <see cref="Amount"/> for matching by shop words, in percent.
	/// </summary>
	public decimal TolerancePct { get; private set; }

	/// <summary>
	/// Gets the number of days between payments: 30 monthly, 91 quarterly, 365 yearly.
	/// </summary>
	public int CadenceDays { get; private set; }

	/// <summary>
	/// Gets a value indicating whether new payments are still matched to it.
	/// </summary>
	public bool Active { get; private set; }

	/// <summary>
	/// Gets when the user accepted it.
	/// </summary>
	public DateTimeOffset CreatedAt { get; private set; }

	/// <summary>
	/// Gets the signature stored when the user stops tracking it, so it is not suggested again.
	/// </summary>
	public string Signature => PayeeMatch.SignatureOf(_seriesKeys);
	#endregion

	#region Constructors
	/// <summary>
	/// Constructor used by EF Core to materialise a stored recurring payment; the persistence layer then sets its values. Domain
	/// code creates instances only through the factory methods. It is <see langword="internal"/> rather than private
	/// because the analyzers report an unused private constructor (S1144) without seeing EF Core's reflection.
	/// </summary>
	internal RecurringPayment()
	{
		Name = "";
	}

	/// <summary>
	/// Initialises a new <see cref="RecurringPayment"/> from already valid values.
	/// </summary>
	private RecurringPayment(RecurringPaymentId id, string name, CategoryId? categoryId, PayeeMatch match, RecurrenceRule rule, bool active, DateTimeOffset createdAt)
		: base(id)
	{
		Name = name;
		CategoryId = categoryId;
		_seriesKeys.AddRange(match.SeriesKeys);
		_tokens.AddRange(match.Tokens);
		Amount = rule.Amount;
		TolerancePct = rule.TolerancePct;
		CadenceDays = rule.CadenceDays;
		Active = active;
		CreatedAt = createdAt;
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates an active fixed payment the user accepted.
	/// </summary>
	/// <param name="id">The new payment's identifier.</param>
	/// <param name="name">The name; required, trimmed.</param>
	/// <param name="categoryId">The category of its payments, or <see langword="null"/> to keep theirs.</param>
	/// <param name="match">How its payments are recognised; at least one series key or shop word.</param>
	/// <param name="rule">The usual amount, tolerance and rhythm; amount and rhythm positive.</param>
	/// <param name="createdAt">When the user accepted it.</param>
	/// <returns>The fixed payment, or <see cref="RecurringPaymentErrors.NameRequired"/>,
	/// <see cref="RecurringPaymentErrors.MatchRequired"/> or <see cref="RecurringPaymentErrors.RuleInvalid"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="match"/> is <see langword="null"/>.</exception>
	public static Result<RecurringPayment> Create(RecurringPaymentId id, string name, CategoryId? categoryId, PayeeMatch match, RecurrenceRule rule, DateTimeOffset createdAt)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(match);
		if (string.IsNullOrWhiteSpace(name))
		{
			return Result.Failure<RecurringPayment>(RecurringPaymentErrors.NameRequired);
		}

		if (match.SeriesKeys.Count == 0 && match.Tokens.Count == 0)
		{
			return Result.Failure<RecurringPayment>(RecurringPaymentErrors.MatchRequired);
		}

		if (rule.Amount <= 0 || rule.CadenceDays <= 0)
		{
			return Result.Failure<RecurringPayment>(RecurringPaymentErrors.RuleInvalid);
		}

		return new RecurringPayment(id, name.Trim(), categoryId, match, rule, active: true, createdAt);
	}

	/// <summary>
	/// Rebuilds a stored fixed payment without the creation checks: stored decisions are kept as they are.
	/// </summary>
	/// <param name="id">The payment's identifier.</param>
	/// <param name="name">The stored name.</param>
	/// <param name="categoryId">The stored category, if any.</param>
	/// <param name="match">The stored series keys and shop words.</param>
	/// <param name="rule">The stored amount, tolerance and rhythm.</param>
	/// <param name="active">Whether it is still matched.</param>
	/// <param name="createdAt">When it was accepted.</param>
	/// <returns>The fixed payment.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static RecurringPayment Rehydrate(RecurringPaymentId id, string name, CategoryId? categoryId, PayeeMatch match, RecurrenceRule rule, bool active, DateTimeOffset createdAt)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(match);
		return new RecurringPayment(id, name, categoryId, match, rule, active, createdAt);
	}
	#endregion

	#region Domain Behaviours
	/// <summary>
	/// Applies the user's edits; each value left <see langword="null"/> (or not positive) is kept.
	/// </summary>
	/// <param name="name">The new name, trimmed, or <see langword="null"/> or blank to keep it.</param>
	/// <param name="categoryId">The new category, or <see langword="null"/> to keep it.</param>
	/// <param name="amount">The new usual amount, or <see langword="null"/> or not positive to keep it.</param>
	/// <param name="cadenceDays">The new rhythm in days, or <see langword="null"/> or not positive to keep it.</param>
	internal void Edit(string? name, CategoryId? categoryId, decimal? amount, int? cadenceDays)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			Name = name.Trim();
		}

		if (categoryId is not null)
		{
			CategoryId = categoryId;
		}

		if (amount is > 0)
		{
			Amount = amount.Value;
		}

		if (cadenceDays is > 0)
		{
			CadenceDays = cadenceDays.Value;
		}
	}

	/// <summary>
	/// Forgets its category, so its payments fall back to their own (used when the category is deleted).
	/// </summary>
	internal void ClearCategory() => CategoryId = null;
	#endregion
}
