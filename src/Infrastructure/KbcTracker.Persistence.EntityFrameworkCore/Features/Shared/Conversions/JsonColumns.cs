using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Converters and comparers for the user's decisions stored as <c>jsonb</c>: payee categories,
///              transaction categories, payee names and dismissed suggestions.
///
///              Each comparer compares and snapshots through the JSON text. Without a deep comparer EF Core would
///              see the same dictionary instance after an in-place change and never save it. Rebuilt dictionaries and
///              sets use ordinal keys, as the domain does.
/// </summary>
internal static class JsonColumns
{
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
