using System.Collections.ObjectModel;

namespace KbcTracker.Domain.Features.Shared.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Everything the user decided in the UI: accepted fixed payments, dismissed suggestions, categories
///              per payee and per transaction, splits of payments, payee names and debts with their repayment plans.
///
///              Kept apart from the transactions so a re-sync never overwrites a decision. The single instance
///              (<see cref="UserDataId.Current"/>) owns the fixed payments; keys are compared ordinally, as they are
///              ids built by the analysis.
/// </summary>
public sealed class UserData : LightweightAggregateRoot<UserDataId>
{
	#region Fields
	/// <summary>
	/// The accepted fixed payments.
	/// </summary>
	private readonly List<RecurringPayment> _recurring = [];

	/// <summary>
	/// The signatures of the suggestions the user rejected.
	/// </summary>
	private readonly HashSet<string> _dismissedSuggestions = new(StringComparer.Ordinal);

	/// <summary>
	/// The category per payee series key.
	/// </summary>
	private readonly Dictionary<string, PayeeAssignment> _payees = new(StringComparer.Ordinal);

	/// <summary>
	/// The category of single transactions.
	/// </summary>
	private readonly Dictionary<TxnId, CategoryId> _txnCategories = [];

	/// <summary>
	/// How single payments are divided into categorised parts.
	/// </summary>
	private readonly Dictionary<TxnId, PaymentSplit> _splits = [];

	/// <summary>
	/// The names the user gave to payees, by series key.
	/// </summary>
	private readonly Dictionary<string, string> _payeeNames = new(StringComparer.Ordinal);

	/// <summary>
	/// The debts the user tracks, in the order they were added.
	/// </summary>
	private readonly List<Debt> _debts = [];
	#endregion

	#region Properties
	/// <summary>
	/// Gets the accepted fixed payments.
	/// </summary>
	public IReadOnlyList<RecurringPayment> Recurring => _recurring.AsReadOnly();

	/// <summary>
	/// Gets the suggestions the user rejected, by signature (see <see cref="PayeeMatch.SignatureOf"/>).
	/// </summary>
	public IReadOnlySet<string> DismissedSuggestions => new ReadOnlySet<string>(_dismissedSuggestions);

	/// <summary>
	/// Gets the category per payee series key; it applies to all transactions of that payee.
	/// </summary>
	public IReadOnlyDictionary<string, PayeeAssignment> Payees => _payees.AsReadOnly();

	/// <summary>
	/// Gets the category of single transactions; it overrides the payee's category.
	/// </summary>
	public IReadOnlyDictionary<TxnId, CategoryId> TxnCategories => _txnCategories.AsReadOnly();

	/// <summary>
	/// Gets how single payments are divided into parts; a split wins over every other category of the payment.
	/// </summary>
	public IReadOnlyDictionary<TxnId, PaymentSplit> Splits => _splits.AsReadOnly();

	/// <summary>
	/// Gets the names the user gave to payees, by series key.
	/// </summary>
	public IReadOnlyDictionary<string, string> PayeeNames => _payeeNames.AsReadOnly();

	/// <summary>
	/// Gets the debts the user tracks (ADR-0010), in the order they were added.
	/// </summary>
	public IReadOnlyList<Debt> Debts => _debts.AsReadOnly();
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises the user's decisions, with nothing decided yet.
	/// </summary>
	private UserData()
		: base(UserDataId.Current)
	{
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates the decisions of a user who has decided nothing yet.
	/// </summary>
	/// <returns>The empty decisions.</returns>
	public static UserData Empty() => new();

	/// <summary>
	/// Rebuilds the stored decisions as they are.
	/// </summary>
	/// <param name="recurring">The accepted fixed payments.</param>
	/// <param name="dismissedSuggestions">The signatures of the rejected suggestions.</param>
	/// <param name="payees">The category per payee series key.</param>
	/// <param name="txnCategories">The category of single transactions.</param>
	/// <param name="payeeNames">The names given to payees.</param>
	/// <param name="splits">How single payments are divided into parts.</param>
	/// <param name="debts">The debts the user tracks.</param>
	/// <returns>The decisions.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static UserData Rehydrate(
		IEnumerable<RecurringPayment> recurring,
		IEnumerable<string> dismissedSuggestions,
		IEnumerable<KeyValuePair<string, PayeeAssignment>> payees,
		IEnumerable<KeyValuePair<TxnId, CategoryId>> txnCategories,
		IEnumerable<KeyValuePair<string, string>> payeeNames,
		IEnumerable<KeyValuePair<TxnId, PaymentSplit>> splits,
		IEnumerable<Debt> debts)
	{
		ArgumentNullException.ThrowIfNull(debts);
		ArgumentNullException.ThrowIfNull(recurring);
		ArgumentNullException.ThrowIfNull(dismissedSuggestions);
		ArgumentNullException.ThrowIfNull(payees);
		ArgumentNullException.ThrowIfNull(txnCategories);
		ArgumentNullException.ThrowIfNull(payeeNames);
		ArgumentNullException.ThrowIfNull(splits);

		var user = new UserData();
		user._recurring.AddRange(recurring);
		user._dismissedSuggestions.UnionWith(dismissedSuggestions);
		foreach (var (key, assignment) in payees)
		{
			user._payees[key] = assignment;
		}

		foreach (var (txnId, categoryId) in txnCategories)
		{
			user._txnCategories[txnId] = categoryId;
		}

		foreach (var (key, name) in payeeNames)
		{
			user._payeeNames[key] = name;
		}

		foreach (var (txnId, split) in splits)
		{
			user._splits[txnId] = split;
		}

		user._debts.AddRange(debts);
		return user;
	}
	#endregion

	#region Fixed Payments
	/// <summary>
	/// Starts tracking a fixed payment the user accepted or marked; it is no longer considered dismissed.
	/// </summary>
	/// <param name="payment">The new fixed payment.</param>
	/// <exception cref="ArgumentNullException"><paramref name="payment"/> is <see langword="null"/>.</exception>
	public void AcceptFixedPayment(RecurringPayment payment)
	{
		ArgumentNullException.ThrowIfNull(payment);
		_dismissedSuggestions.Remove(payment.Signature);
		_recurring.Add(payment);
	}

	/// <summary>
	/// Rejects a suggestion, so it is not suggested again.
	/// </summary>
	/// <param name="signature">The suggestion's signature.</param>
	/// <exception cref="ArgumentException"><paramref name="signature"/> is blank.</exception>
	public void DismissSuggestion(string signature)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(signature);
		_dismissedSuggestions.Add(signature);
	}

	/// <summary>
	/// Applies the user's edits to a fixed payment; each value left <see langword="null"/> is kept.
	/// </summary>
	/// <param name="id">The fixed payment.</param>
	/// <param name="name">The new name, or <see langword="null"/> or blank to keep it.</param>
	/// <param name="categoryId">The new category, or <see langword="null"/> to keep it.</param>
	/// <param name="amount">The new usual amount, or <see langword="null"/> or not positive to keep it.</param>
	/// <param name="cadenceDays">The new rhythm in days, or <see langword="null"/> or not positive to keep it.</param>
	/// <returns>Success, or <see cref="RecurringPaymentErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/>
	/// failure.</returns>
	public Result UpdateFixedPayment(RecurringPaymentId id, string? name, CategoryId? categoryId, decimal? amount, int? cadenceDays)
	{
		var payment = _recurring.Find(p => p.Id == id);
		if (payment is null)
		{
			return Result.Failure(RecurringPaymentErrors.NotFound, ResultExceptionType.NotFound);
		}

		payment.Edit(name, categoryId, amount, cadenceDays);
		return Result.Success();
	}

	/// <summary>
	/// Stops tracking a fixed payment and dismisses it, so it is not suggested again.
	/// </summary>
	/// <param name="id">The fixed payment.</param>
	/// <returns>Success, or <see cref="RecurringPaymentErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/>
	/// failure.</returns>
	public Result RemoveFixedPayment(RecurringPaymentId id)
	{
		var payment = _recurring.Find(p => p.Id == id);
		if (payment is null)
		{
			return Result.Failure(RecurringPaymentErrors.NotFound, ResultExceptionType.NotFound);
		}

		_recurring.Remove(payment);
		_dismissedSuggestions.Add(payment.Signature);
		return Result.Success();
	}
	#endregion

	#region Categories
	/// <summary>
	/// Sets the confirmed category of every payment of a payee.
	/// </summary>
	/// <param name="seriesKey">The payee series key.</param>
	/// <param name="categoryId">The category, or <see langword="null"/> to confirm without choosing one.</param>
	/// <exception cref="ArgumentException"><paramref name="seriesKey"/> is blank.</exception>
	public void AssignPayeeCategory(string seriesKey, CategoryId? categoryId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
		_payees[seriesKey] = new PayeeAssignment(categoryId, Confirmed: true);
	}

	/// <summary>
	/// Puts back a payee's previous assignment, undoing <see cref="AssignPayeeCategory"/> when the user chose
	/// "only this one".
	/// </summary>
	/// <param name="seriesKey">The payee series key.</param>
	/// <param name="previous">The assignment before the change, or <see langword="null"/> when there was none.</param>
	/// <exception cref="ArgumentException"><paramref name="seriesKey"/> is blank.</exception>
	public void RestorePayeeCategory(string seriesKey, PayeeAssignment? previous)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
		if (previous is null)
		{
			_payees.Remove(seriesKey);
		}
		else
		{
			_payees[seriesKey] = previous;
		}
	}

	/// <summary>
	/// Sets the category of one transaction only; it overrides the payee's category.
	/// </summary>
	/// <param name="txnId">The transaction.</param>
	/// <param name="categoryId">The category.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public void AssignTransactionCategory(TxnId txnId, CategoryId categoryId)
	{
		ArgumentNullException.ThrowIfNull(txnId);
		ArgumentNullException.ThrowIfNull(categoryId);
		_txnCategories[txnId] = categoryId;
	}

	/// <summary>
	/// Points a payee rule to another category without the user having chosen it, for instance when a category is
	/// merged into the default tree.
	/// </summary>
	/// <param name="seriesKey">The payee series key.</param>
	/// <param name="categoryId">The category that takes over.</param>
	/// <param name="confirmed">Whether the rule counts as confirmed; <see langword="false"/> puts the payee in the
	/// review queue.</param>
	/// <exception cref="ArgumentException"><paramref name="seriesKey"/> is blank.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="categoryId"/> is <see langword="null"/>.</exception>
	public void ReassignPayee(string seriesKey, CategoryId categoryId, bool confirmed)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
		ArgumentNullException.ThrowIfNull(categoryId);
		_payees[seriesKey] = new PayeeAssignment(categoryId, confirmed);
	}

	/// <summary>
	/// Points a fixed payment to another category, keeping everything else about it.
	/// </summary>
	/// <param name="id">The fixed payment.</param>
	/// <param name="categoryId">The category that takes over.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public void ReassignFixedPayment(RecurringPaymentId id, CategoryId categoryId)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(categoryId);
		_recurring.Find(p => p.Id == id)?.ReassignCategory(categoryId);
	}

	/// <summary>
	/// Moves everything that refers to a category to another one, used when the category is deleted: its payee
	/// rules, the choices made for single transactions and its fixed payments now point to <paramref name="to"/>.
	/// A payee rule keeps its confirmed state, except when it moves to <c>other</c>: that is no decision of the user's, so
	/// the rule becomes unconfirmed and its payments show up in the review queue.
	/// </summary>
	/// <param name="from">The category being removed.</param>
	/// <param name="to">The category that takes over: the parent, or <c>other</c> for a top-level category.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public void ReassignCategory(CategoryId from, CategoryId to)
	{
		ArgumentNullException.ThrowIfNull(from);
		ArgumentNullException.ThrowIfNull(to);
		foreach (var (key, assignment) in _payees.Where(kv => kv.Value.CategoryId == from).ToList())
		{
			_payees[key] = assignment with { CategoryId = to, Confirmed = assignment.Confirmed && to != CategoryId.Other };
		}

		foreach (var txnId in _txnCategories.Where(kv => kv.Value == from).Select(kv => kv.Key).ToList())
		{
			_txnCategories[txnId] = to;
		}

		foreach (var (txnId, split) in _splits.ToList())
		{
			_splits[txnId] = split.ReassignCategory(from, to);
		}

		foreach (var payment in _recurring.Where(p => p.CategoryId == from))
		{
			payment.ReassignCategory(to);
		}
	}
	#endregion

	#region Splits
	/// <summary>
	/// Divides one payment into categorised parts, replacing an earlier split of it. The payment's own category is kept,
	/// so removing the split brings it back.
	/// </summary>
	/// <param name="txnId">The transaction.</param>
	/// <param name="split">The parts, already checked against the payment's amount.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public void SplitPayment(TxnId txnId, PaymentSplit split)
	{
		ArgumentNullException.ThrowIfNull(txnId);
		ArgumentNullException.ThrowIfNull(split);
		_splits[txnId] = split;
	}

	/// <summary>
	/// Moves the lines of one payment's split from one category to another, for instance when a category is merged
	/// into the default tree and each payment finds its own heir. Does nothing when the payment is not split.
	/// </summary>
	/// <param name="txnId">The transaction.</param>
	/// <param name="from">The category being replaced.</param>
	/// <param name="to">The category that takes over.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public void ReassignSplitLines(TxnId txnId, CategoryId from, CategoryId to)
	{
		ArgumentNullException.ThrowIfNull(txnId);
		ArgumentNullException.ThrowIfNull(from);
		ArgumentNullException.ThrowIfNull(to);
		if (_splits.TryGetValue(txnId, out var split))
		{
			_splits[txnId] = split.ReassignCategory(from, to);
		}
	}

	/// <summary>
	/// Removes the split of a payment, which falls back to its normal category.
	/// </summary>
	/// <param name="txnId">The transaction.</param>
	/// <returns>Success, or <see cref="PaymentSplitErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/>
	/// failure when the payment is not split.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="txnId"/> is <see langword="null"/>.</exception>
	public Result RemoveSplit(TxnId txnId)
	{
		ArgumentNullException.ThrowIfNull(txnId);
		return _splits.Remove(txnId)
			? Result.Success()
			: Result.Failure(PaymentSplitErrors.NotFound, ResultExceptionType.NotFound);
	}
	#endregion

	#region Debts
	/// <summary>
	/// Starts tracking a debt.
	/// </summary>
	/// <param name="id">The new debt's identifier.</param>
	/// <param name="terms">What the user says about the debt.</param>
	/// <returns>Success, or the <see cref="DebtErrors"/> that rejects the terms, for instance
	/// <see cref="DebtErrors.ReceivedPaymentTaken"/> when another debt tracks the received payment.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public Result AddDebt(DebtId id, DebtTerms terms)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(terms);
		if (IsTrackedByOther(terms.ReceivedPaymentId, id))
		{
			return Result.Failure(DebtErrors.ReceivedPaymentTaken);
		}

		var debt = Debt.Create(id, terms);
		if (debt.IsFailure)
		{
			return Result.Failure(debt.Error, debt.ResultExceptionType);
		}

		_debts.Add(debt.Value);
		return Result.Success();
	}

	/// <summary>
	/// Replaces what the user said about a debt, keeping the payments added or excluded by hand.
	/// </summary>
	/// <param name="id">The debt.</param>
	/// <param name="terms">The new terms.</param>
	/// <returns>Success, <see cref="DebtErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/> failure, or the
	/// <see cref="DebtErrors"/> that rejects the terms.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public Result UpdateDebt(DebtId id, DebtTerms terms)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(terms);
		var debt = _debts.Find(d => d.Id == id);
		if (debt is null)
		{
			return Result.Failure(DebtErrors.NotFound, ResultExceptionType.NotFound);
		}

		return IsTrackedByOther(terms.ReceivedPaymentId, id) ? Result.Failure(DebtErrors.ReceivedPaymentTaken) : debt.Revise(terms);
	}

	/// <summary>
	/// Stops tracking a debt.
	/// </summary>
	/// <param name="id">The debt.</param>
	/// <returns>Success, or <see cref="DebtErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/> failure.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
	public Result RemoveDebt(DebtId id)
	{
		ArgumentNullException.ThrowIfNull(id);
		return _debts.RemoveAll(d => d.Id == id) > 0
			? Result.Success()
			: Result.Failure(DebtErrors.NotFound, ResultExceptionType.NotFound);
	}

	/// <summary>
	/// Says that a payment repays a debt, whatever the automatic matching finds.
	/// </summary>
	/// <param name="id">The debt.</param>
	/// <param name="paymentId">The payment.</param>
	/// <returns>Success, <see cref="DebtErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/> failure, or
	/// <see cref="DebtErrors.PaymentTaken"/> when another debt already includes the payment.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public Result IncludeRepayment(DebtId id, TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(paymentId);
		var debt = _debts.Find(d => d.Id == id);
		if (debt is null)
		{
			return Result.Failure(DebtErrors.NotFound, ResultExceptionType.NotFound);
		}

		if (_debts.Exists(d => d.Id != id && d.IncludedPayments.Contains(paymentId)))
		{
			return Result.Failure(DebtErrors.PaymentTaken);
		}

		debt.Include(paymentId);
		return Result.Success();
	}

	/// <summary>
	/// Says that a payment does not repay a debt, whatever the automatic matching finds.
	/// </summary>
	/// <param name="id">The debt.</param>
	/// <param name="paymentId">The payment.</param>
	/// <returns>Success, or <see cref="DebtErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/> failure.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public Result ExcludeRepayment(DebtId id, TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(paymentId);
		var debt = _debts.Find(d => d.Id == id);
		if (debt is null)
		{
			return Result.Failure(DebtErrors.NotFound, ResultExceptionType.NotFound);
		}

		debt.Exclude(paymentId);
		return Result.Success();
	}

	/// <summary>
	/// Forgets what the user said about a payment and a debt, so the automatic matching decides again.
	/// </summary>
	/// <param name="id">The debt.</param>
	/// <param name="paymentId">The payment.</param>
	/// <returns>Success, or <see cref="DebtErrors.NotFound"/> as a <see cref="ResultExceptionType.NotFound"/> failure.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public Result ResetRepayment(DebtId id, TxnId paymentId)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(paymentId);
		var debt = _debts.Find(d => d.Id == id);
		if (debt is null)
		{
			return Result.Failure(DebtErrors.NotFound, ResultExceptionType.NotFound);
		}

		debt.Reset(paymentId);
		return Result.Success();
	}
	#endregion

	#region Payees
	/// <summary>
	/// Gives a payee the name the user prefers to the one read from the bank label.
	/// </summary>
	/// <param name="seriesKey">The payee series key.</param>
	/// <param name="name">The new name; trimmed.</param>
	/// <returns>Success, or <see cref="UserDataErrors.PayeeNameRequired"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="seriesKey"/> is blank.</exception>
	public Result RenamePayee(string seriesKey, string? name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
		if (string.IsNullOrWhiteSpace(name))
		{
			return Result.Failure(UserDataErrors.PayeeNameRequired);
		}

		_payeeNames[seriesKey] = name.Trim();
		return Result.Success();
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Determines whether a debt other than the given one tracks a received payment.
	/// </summary>
	private bool IsTrackedByOther(TxnId? receivedPaymentId, DebtId except) =>
		receivedPaymentId is not null && _debts.Exists(d => d.Id != except && d.ReceivedPaymentId == receivedPaymentId);
	#endregion
}
