using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Converters and comparers for the user's decisions stored as <c>jsonb</c>: payee categories,
///              transaction categories, splits of payments, debts, payee names and dismissed suggestions, and the accounts
///              of the bank connection.
///
///              Each comparer compares and snapshots through the JSON text. Without a deep comparer EF Core would
///              see the same dictionary instance after an in-place change and never save it. Rebuilt dictionaries and
///              sets use ordinal keys, as the domain does.
/// </summary>
internal static class JsonColumns
{
	#region Constants
	/// <summary>
	/// The shape of <see cref="StoredDebt"/> this version writes.
	/// </summary>
	private const int StoredDebtVersion = 2;
	#endregion

	#region Static Instances
	/// <summary>
	/// Converts the payee categories.
	/// </summary>
	public static readonly ValueConverter<Dictionary<string, PayeeAssignment>, string> PayeesConverter =
		new(value => WritePayees(value), json => ReadPayees(json));

	/// <summary>
	/// Compares the payee categories by content.
	/// </summary>
	public static readonly ValueComparer<Dictionary<string, PayeeAssignment>> PayeesComparer =
		new((a, b) => WritePayees(a!) == WritePayees(b!), v => WritePayees(v).GetHashCode(StringComparison.Ordinal), v => ReadPayees(WritePayees(v)));

	/// <summary>
	/// Converts the categories chosen for single transactions.
	/// </summary>
	public static readonly ValueConverter<Dictionary<TxnId, CategoryId>, string> TxnCategoriesConverter =
		new(value => WriteTxnCategories(value), json => ReadTxnCategories(json));

	/// <summary>
	/// Compares the transaction categories by content.
	/// </summary>
	public static readonly ValueComparer<Dictionary<TxnId, CategoryId>> TxnCategoriesComparer =
		new((a, b) => WriteTxnCategories(a!) == WriteTxnCategories(b!), v => WriteTxnCategories(v).GetHashCode(StringComparison.Ordinal), v => ReadTxnCategories(WriteTxnCategories(v)));

	/// <summary>
	/// Converts the splits of payments.
	/// </summary>
	public static readonly ValueConverter<Dictionary<TxnId, PaymentSplit>, string> SplitsConverter =
		new(value => WriteSplits(value), json => ReadSplits(json));

	/// <summary>
	/// Compares the splits by content.
	/// </summary>
	public static readonly ValueComparer<Dictionary<TxnId, PaymentSplit>> SplitsComparer =
		new((a, b) => WriteSplits(a!) == WriteSplits(b!), v => WriteSplits(v).GetHashCode(StringComparison.Ordinal), v => ReadSplits(WriteSplits(v)));

	/// <summary>
	/// Converts the debts.
	/// </summary>
	public static readonly ValueConverter<List<Debt>, string> DebtsConverter =
		new(value => WriteDebts(value), json => ReadDebts(json));

	/// <summary>
	/// Compares the debts by content.
	/// </summary>
	public static readonly ValueComparer<List<Debt>> DebtsComparer =
		new((a, b) => WriteDebts(a!) == WriteDebts(b!), v => WriteDebts(v).GetHashCode(StringComparison.Ordinal), v => ReadDebts(WriteDebts(v)));

	/// <summary>
	/// Converts the accounts of a bank connection.
	/// </summary>
	public static readonly ValueConverter<List<OwnAccount>, string> AccountsConverter =
		new(value => WriteAccounts(value), json => ReadAccounts(json));

	/// <summary>
	/// Compares the accounts of a bank connection by content.
	/// </summary>
	public static readonly ValueComparer<List<OwnAccount>> AccountsComparer =
		new((a, b) => WriteAccounts(a!) == WriteAccounts(b!), v => WriteAccounts(v).GetHashCode(StringComparison.Ordinal), v => ReadAccounts(WriteAccounts(v)));

	/// <summary>
	/// Converts the payee names.
	/// </summary>
	public static readonly ValueConverter<Dictionary<string, string>, string> NamesConverter =
		new(value => WriteNames(value), json => ReadNames(json));

	/// <summary>
	/// Compares the payee names by content.
	/// </summary>
	public static readonly ValueComparer<Dictionary<string, string>> NamesComparer =
		new((a, b) => WriteNames(a!) == WriteNames(b!), v => WriteNames(v).GetHashCode(StringComparison.Ordinal), v => ReadNames(WriteNames(v)));

	/// <summary>
	/// Converts the dismissed suggestion signatures.
	/// </summary>
	public static readonly ValueConverter<HashSet<string>, string> SignaturesConverter =
		new(value => WriteSignatures(value), json => ReadSignatures(json));

	/// <summary>
	/// Compares the dismissed suggestion signatures by content.
	/// </summary>
	public static readonly ValueComparer<HashSet<string>> SignaturesComparer =
		new((a, b) => WriteSignatures(a!) == WriteSignatures(b!), v => WriteSignatures(v).GetHashCode(StringComparison.Ordinal), v => ReadSignatures(WriteSignatures(v)));
	#endregion

	#region Private Helpers
	/// <summary>
	/// Writes the payee categories as JSON.
	/// </summary>
	private static string WritePayees(Dictionary<string, PayeeAssignment> value) =>
		JsonSerializer.Serialize(value.ToDictionary(kv => kv.Key, kv => new StoredPayeeAssignment(kv.Value.CategoryId?.Value, kv.Value.Confirmed), StringComparer.Ordinal));

	/// <summary>
	/// Reads the payee categories from JSON.
	/// </summary>
	private static Dictionary<string, PayeeAssignment> ReadPayees(string json) =>
		(JsonSerializer.Deserialize<Dictionary<string, StoredPayeeAssignment>>(json) ?? [])
			.ToDictionary(kv => kv.Key, kv => new PayeeAssignment(kv.Value.CategoryId is null ? null : new CategoryId(kv.Value.CategoryId), kv.Value.Confirmed), StringComparer.Ordinal);

	/// <summary>
	/// Writes the transaction categories as JSON, keyed by transaction id.
	/// </summary>
	private static string WriteTxnCategories(Dictionary<TxnId, CategoryId> value) =>
		JsonSerializer.Serialize(value.ToDictionary(kv => kv.Key.Value, kv => kv.Value.Value, StringComparer.Ordinal));

	/// <summary>
	/// Reads the transaction categories from JSON.
	/// </summary>
	private static Dictionary<TxnId, CategoryId> ReadTxnCategories(string json) =>
		(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []).ToDictionary(kv => new TxnId(kv.Key), kv => new CategoryId(kv.Value));

	/// <summary>
	/// Writes the splits as JSON, keyed by transaction id.
	/// </summary>
	private static string WriteSplits(Dictionary<TxnId, PaymentSplit> value) =>
		JsonSerializer.Serialize(value.ToDictionary(
			kv => kv.Key.Value,
			kv => kv.Value.Lines.Select(l => new StoredSplitLine(l.Amount, l.CategoryId.Value, l.Label)).ToList(),
			StringComparer.Ordinal));

	/// <summary>
	/// Reads the splits from JSON.
	/// </summary>
	private static Dictionary<TxnId, PaymentSplit> ReadSplits(string json) =>
		(JsonSerializer.Deserialize<Dictionary<string, List<StoredSplitLine>>>(json) ?? [])
			.ToDictionary(kv => new TxnId(kv.Key), kv => PaymentSplit.Rehydrate(kv.Value.Select(l => new SplitLine(l.Amount, new CategoryId(l.CategoryId), l.Label))));

	/// <summary>
	/// Writes the debts as a JSON array of <see cref="StoredDebt"/>; the manual payment lists are sorted so equal debts
	/// give equal text.
	/// </summary>
	private static string WriteDebts(List<Debt> value) =>
		JsonSerializer.Serialize(value.Select(d => new StoredDebt(
			StoredDebtVersion, d.Id.Value, d.Kind.ToString(), d.Lender, d.Iban, d.ReceivedPaymentId?.Value, d.Principal, d.SeriesKey,
			d.Plan is null ? null : new StoredRepaymentPlan(d.Plan.Amount, d.Plan.Start, d.Plan.Frequency?.ToString(), d.Plan.DayOfMonth, d.Plan.InstalmentAmount, d.Plan.Count),
			[.. d.IncludedPayments.Select(p => p.Value).Order(StringComparer.Ordinal)],
			[.. d.ExcludedPayments.Select(p => p.Value).Order(StringComparer.Ordinal)], d.BorrowedOn)));

	/// <summary>
	/// Reads the debts from JSON.
	/// </summary>
	private static List<Debt> ReadDebts(string json) =>
		[.. (JsonSerializer.Deserialize<List<StoredDebt>>(json) ?? []).Select(d =>
		{
			var terms = new DebtTerms(Enum.Parse<DebtKind>(d.Kind), d.Lender, d.Iban, d.ReceivedPaymentId is null ? null : new TxnId(d.ReceivedPaymentId),
				d.Principal, d.SeriesKey, ReadPlan(d.Plan), d.BorrowedOn ?? ReadPlan(d.Plan)?.Start ?? new DateOnly(RepaymentPlan.MinYear, 1, 1));
			return Debt.Rehydrate(new DebtId(d.Id), terms, d.Included.Select(p => new TxnId(p)), d.Excluded.Select(p => new TxnId(p)));
		})];

	/// <summary>
	/// Reads a stored repayment plan.
	/// </summary>
	private static RepaymentPlan? ReadPlan(StoredRepaymentPlan? plan)
	{
		if (plan is null)
		{
			return null;
		}

		RepaymentFrequency? frequency = plan.Frequency is null ? null : Enum.Parse<RepaymentFrequency>(plan.Frequency);
		return RepaymentPlan.Rehydrate(plan.Amount, plan.Start, frequency, plan.DayOfMonth, plan.InstalmentAmount, plan.Count);
	}

	/// <summary>
	/// Writes the accounts of a bank connection as JSON.
	/// </summary>
	private static string WriteAccounts(List<OwnAccount> value) => JsonSerializer.Serialize(value);

	/// <summary>
	/// Reads the accounts of a bank connection from JSON.
	/// </summary>
	private static List<OwnAccount> ReadAccounts(string json) => JsonSerializer.Deserialize<List<OwnAccount>>(json) ?? [];

	/// <summary>
	/// Writes the payee names as JSON.
	/// </summary>
	private static string WriteNames(Dictionary<string, string> value) => JsonSerializer.Serialize(value);

	/// <summary>
	/// Reads the payee names from JSON.
	/// </summary>
	private static Dictionary<string, string> ReadNames(string json) =>
		new(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.Ordinal);

	/// <summary>
	/// Writes the dismissed suggestion signatures as a JSON array.
	/// </summary>
	private static string WriteSignatures(HashSet<string> value) => JsonSerializer.Serialize(value);

	/// <summary>
	/// Reads the dismissed suggestion signatures from a JSON array.
	/// </summary>
	private static HashSet<string> ReadSignatures(string json) =>
		new(JsonSerializer.Deserialize<string[]>(json) ?? [], StringComparer.Ordinal);
	#endregion
}
