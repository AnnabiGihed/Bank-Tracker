namespace KbcTracker.Infrastructure.Features.Legacy.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The user's decisions as stored in <c>userdata.json</c>, in the file's existing shape.
/// </summary>
public sealed record UserDataDocument
{
	#region Properties
	/// <summary>
	/// Gets the accepted fixed payments.
	/// </summary>
	public IReadOnlyList<RecurringPaymentDocument> Recurring { get; init; } = [];

	/// <summary>
	/// Gets the signatures of the rejected suggestions.
	/// </summary>
	public IReadOnlyList<string> DismissedSuggestions { get; init; } = [];

	/// <summary>
	/// Gets the category per payee series key.
	/// </summary>
	public IReadOnlyDictionary<string, PayeeAssignmentDocument> Payees { get; init; } = new Dictionary<string, PayeeAssignmentDocument>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the category id per transaction id.
	/// </summary>
	public IReadOnlyDictionary<string, string> TxnCategories { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the names given to payees, by series key.
	/// </summary>
	public IReadOnlyDictionary<string, string> PayeeNames { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
	#endregion

	#region Public Methods
	/// <summary>
	/// Stores the user's decisions.
	/// </summary>
	/// <param name="user">The decisions.</param>
	/// <returns>The document.</returns>
	public static UserDataDocument From(UserData user) => new()
	{
		Recurring = [.. user.Recurring.Select(RecurringPaymentDocument.From)],
		DismissedSuggestions = [.. user.DismissedSuggestions],
		Payees = user.Payees.ToDictionary(kv => kv.Key, kv => PayeeAssignmentDocument.From(kv.Value), StringComparer.Ordinal),
		TxnCategories = user.TxnCategories.ToDictionary(kv => kv.Key.Value, kv => kv.Value.Value, StringComparer.Ordinal),
		PayeeNames = user.PayeeNames.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
	};

	/// <summary>
	/// Rebuilds the stored decisions as they are. Entries older versions could store but nothing can refer to (a
	/// blank key, an empty payee entry) are left out, and a transaction category that is blank or missing keeps its
	/// old meaning, "Other".
	/// </summary>
	/// <returns>The decisions.</returns>
	public UserData ToDomain() => UserData.Rehydrate(
		(Recurring ?? []).OfType<RecurringPaymentDocument>().Select(r => r.ToDomain()),
		(DismissedSuggestions ?? []).Where(s => !string.IsNullOrWhiteSpace(s)),
		(Payees ?? new Dictionary<string, PayeeAssignmentDocument>(StringComparer.Ordinal))
			.Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is not null)
			.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.ToDomain())),
		(TxnCategories ?? new Dictionary<string, string>(StringComparer.Ordinal))
			.Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
			.Select(kv => KeyValuePair.Create(new TxnId(kv.Key), LegacyValues.Category(kv.Value) ?? CategoryId.Other)),
		(PayeeNames ?? new Dictionary<string, string>(StringComparer.Ordinal)).Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is not null),
		[]);
	#endregion
}
