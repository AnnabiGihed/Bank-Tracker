using System.Collections.ObjectModel;

namespace KbcTracker.Domain.Features.Shared.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Everything the user decided in the UI: accepted fixed payments, dismissed suggestions, categories
///              per payee and per transaction, and payee names.
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
	/// The names the user gave to payees, by series key.
	/// </summary>
	private readonly Dictionary<string, string> _payeeNames = new(StringComparer.Ordinal);
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
	/// Gets the names the user gave to payees, by series key.
	/// </summary>
	public IReadOnlyDictionary<string, string> PayeeNames => _payeeNames.AsReadOnly();
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
	/// <returns>The decisions.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static UserData Rehydrate(
		IEnumerable<RecurringPayment> recurring,
		IEnumerable<string> dismissedSuggestions,
		IEnumerable<KeyValuePair<string, PayeeAssignment>> payees,
		IEnumerable<KeyValuePair<TxnId, CategoryId>> txnCategories,
		IEnumerable<KeyValuePair<string, string>> payeeNames)
	{
		ArgumentNullException.ThrowIfNull(recurring);
		ArgumentNullException.ThrowIfNull(dismissedSuggestions);
		ArgumentNullException.ThrowIfNull(payees);
		ArgumentNullException.ThrowIfNull(txnCategories);
		ArgumentNullException.ThrowIfNull(payeeNames);

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
	/// Forgets a deleted category everywhere: its payees and transactions go back to automatic categorisation (and
	/// to the review queue), and its fixed payments use their payments' own category.
	/// </summary>
	/// <param name="categoryId">The deleted category.</param>
	/// <exception cref="ArgumentNullException"><paramref name="categoryId"/> is <see langword="null"/>.</exception>
	public void ForgetCategory(CategoryId categoryId)
	{
		ArgumentNullException.ThrowIfNull(categoryId);
		foreach (var key in _payees.Where(kv => kv.Value.CategoryId == categoryId).Select(kv => kv.Key).ToList())
		{
			_payees.Remove(key);
		}

		foreach (var txnId in _txnCategories.Where(kv => kv.Value == categoryId).Select(kv => kv.Key).ToList())
		{
			_txnCategories.Remove(txnId);
		}

		foreach (var payment in _recurring.Where(p => p.CategoryId == categoryId))
		{
			payment.ClearCategory();
		}
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
}
