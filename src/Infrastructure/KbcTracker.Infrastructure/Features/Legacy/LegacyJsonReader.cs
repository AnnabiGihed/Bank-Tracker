using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.Infrastructure.Features.Legacy.Documents;

namespace KbcTracker.Infrastructure.Features.Legacy;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reads a stopped web app's JSON archive for import without creating, seeding or changing files.
///              Missing required files and malformed documents fail rather than becoming an empty history. Only
///              <c>userdata.json</c> may be missing: the web app wrote it on the user's first decision, so an archive
///              without one means no decisions yet, exactly as the web app read it.
/// </summary>
public static class LegacyJsonReader
{
	#region Static Instances
	/// <summary>The legacy files write property names as declared and enums by name.</summary>
	private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Public Methods
	/// <summary>Reads the complete archive before a database import may start.</summary>
	/// <param name="directory">The archive folder, with transactions and categories JSON files, and userdata and
	/// session JSON files when they were written.</param>
	/// <param name="cancellationToken">Cancels file reads.</param>
	/// <returns>The snapshot, preserving stored amounts, original bank JSON and legacy identifier rules.</returns>
	/// <exception cref="ArgumentException">The directory is blank.</exception>
	/// <exception cref="IOException">A required file is missing or unreadable.</exception>
	/// <exception cref="InvalidDataException">A document is null, contains null rows or has ambiguous identifiers.</exception>
	/// <exception cref="JsonException">A document cannot be parsed using the legacy JSON format.</exception>
	public static async Task<LegacyJsonSnapshot> ReadAsync(string directory, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		var transactions = await ReadRequiredAsync<List<TxnDocument>>(directory, "transactions.json", cancellationToken).ConfigureAwait(false);
		var categories = await ReadRequiredAsync<List<CategoryDocument>>(directory, "categories.json", cancellationToken).ConfigureAwait(false);
		var user = File.Exists(Path.Combine(directory, "userdata.json"))
			? await ReadRequiredAsync<UserDataDocument>(directory, "userdata.json", cancellationToken).ConfigureAwait(false)
			: new UserDataDocument();
		ValidateRows(transactions, "transactions.json");
		ValidateRows(categories, "categories.json");
		ValidateRows(user.Recurring ?? [], "userdata.json");
		if (categories.Any(category => !category.HasId))
		{
			throw new InvalidDataException("categories.json contains a category without an identifier; repair the archive before importing.");
		}

		var history = transactions.Select(transaction => transaction.ToDomain()).ToArray();
		var definitions = categories.Select(category => category.ToDomain()).ToArray();
		RequireUnique(history.Select(transaction => transaction.Id.Value), "transactions.json");
		RequireUnique(definitions.Select(category => category.Id.Value), "categories.json");
		var decisions = user.ToDomain();
		RequireUnique(decisions.Recurring.Select(payment => payment.Id.Value), "userdata.json");
		var sessionPath = Path.Combine(directory, "session.json");
		var session = File.Exists(sessionPath)
			? (await ReadRequiredAsync<SessionDocument>(directory, "session.json", cancellationToken).ConfigureAwait(false)).ToDomain()
			: null;
		return new(history, definitions, decisions, session);
	}
	#endregion

	#region Private Helpers
	/// <summary>Rejects a missing file or JSON null instead of silently synthesising data.</summary>
	private static async Task<T> ReadRequiredAsync<T>(string directory, string name, CancellationToken cancellationToken)
		where T : class
	{
		var stream = File.OpenRead(Path.Combine(directory, name));
		await using (stream.ConfigureAwait(false))
		{
			return await JsonSerializer.DeserializeAsync<T>(stream, Json, cancellationToken).ConfigureAwait(false)
				?? throw new InvalidDataException($"{name} contains null; repair the archive before importing.");
		}
	}

	/// <summary>Rejects null array members rather than dropping historical records.</summary>
	private static void ValidateRows<T>(IEnumerable<T> documents, string name)
	{
		if (documents.Any(document => document is null))
		{
			throw new InvalidDataException($"{name} contains a null row; repair the archive before importing.");
		}
	}

	/// <summary>Rejects ambiguous rows before any database state can change.</summary>
	private static void RequireUnique(IEnumerable<string> identifiers, string name)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		if (identifiers.Any(identifier => !seen.Add(identifier)))
		{
			throw new InvalidDataException($"{name} contains duplicate identifiers; repair the archive before importing.");
		}
	}
	#endregion
}
