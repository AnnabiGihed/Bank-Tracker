using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KbcTracker.Web.Features.Shared.Storage;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The web host's <see cref="IStore"/>: JSON files in a data folder that survives rebuilds (default
///              <c>%LOCALAPPDATA%\KbcTracker</c>, or the <c>DataDirectory</c> setting).
///
///              Transactions are only ever added or updated, never removed, so the history keeps growing past KBC's
///              90-day window; a daily copy is kept in <c>backups</c> (the last 30). Files are written to a temporary
///              file first and then swapped in, so a crash never leaves a half-written file. Writes are serialised
///              with a lock because a page and the auto-sync can save at the same time. The domain is mapped to
///              storage documents that keep the files' shape. Replaced by the SQLite store in step 3 of ADR-0003.
/// </summary>
public sealed partial class Store : IStore
{
	#region Static Instances
	/// <summary>
	/// Serializer settings of every file: indented, enums by name.
	/// </summary>
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Fields
	/// <summary>
	/// Serialises writes, so two saves never interleave.
	/// </summary>
	private readonly Lock _lock = new();
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="Store"/> and creates the data folder. Data from versions that kept it next to
	/// the binaries (<c>bin/…/data</c>) is copied into the data folder once, so a rebuild cannot wipe it.
	/// </summary>
	/// <param name="config">Configuration holding the optional <c>DataDirectory</c> setting (environment variables are expanded).</param>
	/// <param name="log">Logger for the data folder in use.</param>
	public Store(IConfiguration config, ILogger<Store> log)
	{
		var configured = config["DataDirectory"];
		Dir = string.IsNullOrWhiteSpace(configured)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KbcTracker")
			: Environment.ExpandEnvironmentVariables(configured);
		Directory.CreateDirectory(Dir);

		var old = Path.Combine(AppContext.BaseDirectory, "data");
		if (!File.Exists(TxnFile) && Directory.Exists(old) && !string.Equals(Path.GetFullPath(old), Path.GetFullPath(Dir), StringComparison.OrdinalIgnoreCase))
		{
			foreach (var file in Directory.GetFiles(old, "*.json"))
			{
				File.Copy(file, Path.Combine(Dir, Path.GetFileName(file)), overwrite: false);
			}

			LogMovedData(log, old, Dir);
		}

		LogDataFolder(log, Dir);
	}
	#endregion

	#region Properties
	/// <summary>
	/// Gets the data folder.
	/// </summary>
	public string Dir { get; }

	/// <summary>
	/// Gets the path of the bank session file.
	/// </summary>
	private string SessionFile => Path.Combine(Dir, "session.json");

	/// <summary>
	/// Gets the path of the transaction history file.
	/// </summary>
	private string TxnFile => Path.Combine(Dir, "transactions.json");

	/// <summary>
	/// Gets the path of the user's decisions file.
	/// </summary>
	private string UserFile => Path.Combine(Dir, "userdata.json");

	/// <summary>
	/// Gets the path of the category definitions file.
	/// </summary>
	private string CategoriesFile => Path.Combine(Dir, "categories.json");

	/// <summary>
	/// Gets the folder of the daily transaction backups.
	/// </summary>
	private string BackupDir => Path.Combine(Dir, "backups");
	#endregion

	#region Public Methods
	/// <summary>
	/// Turns a category name into its id; kept here because the <c>/api</c> endpoint calls it.
	/// </summary>
	/// <param name="name">The category name.</param>
	/// <returns>The id (see <see cref="CategoryDefaults.Slug"/>).</returns>
	public static string Slug(string name) => CategoryDefaults.Slug(name);

	/// <summary>
	/// Exports every transaction, oldest first, as semicolon-separated values with a header row, for spreadsheets.
	/// </summary>
	/// <param name="analysis">The analysis whose transactions, categories and fixed payments are exported.</param>
	/// <returns>The CSV text; amounts use a dot as decimal separator, and text fields are quoted.</returns>
	public static string ToCsv(Analysis analysis)
	{
		var csv = new StringBuilder("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description\n");
		foreach (var item in analysis.Items.OrderBy(i => i.T.Date))
		{
			csv.Append(CultureInfo.InvariantCulture,
				$"{item.T.Date:yyyy-MM-dd};{item.T.Amount};{item.T.Currency};{Quote(item.Name)};{Quote(item.Category.Name)};{(item.CategoryConfirmed ? "yes" : "")};{Quote(item.Recurring?.Name ?? "")};{Quote(item.T.Description)}\n");
		}

		return csv.ToString();
	}
	#endregion

	#region IStore Implementation
	/// <inheritdoc />
	public SessionInfo? LoadSession() => Read<SessionDocument>(SessionFile)?.ToDomain();

	/// <inheritdoc />
	public void SaveSession(SessionInfo session)
	{
		lock (_lock)
		{
			Write(SessionFile, SessionDocument.From(session));
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<Txn> LoadTransactions() => [.. (Read<List<TxnDocument>>(TxnFile) ?? []).OfType<TxnDocument>().Select(d => d.ToDomain())];

	/// <inheritdoc />
	/// <remarks>Copies the current file to the daily backup before writing, and stores the transactions newest first.</remarks>
	public int Merge(IEnumerable<Txn> incoming)
	{
		lock (_lock)
		{
			var byId = LoadTransactions().ToDictionary(t => t.Id);
			var before = byId.Count;
			foreach (var txn in incoming)
			{
				byId[txn.Id] = txn;
			}

			if (File.Exists(TxnFile))
			{
				Backup();
			}

			Write(TxnFile, byId.Values.OrderByDescending(t => t.Date).Select(TxnDocument.From).ToList());
			return byId.Count - before;
		}
	}

	/// <inheritdoc />
	public UserData LoadUserData() => Read<UserDataDocument>(UserFile)?.ToDomain() ?? UserData.Empty();

	/// <inheritdoc />
	public void SaveUserData(UserData data)
	{
		lock (_lock)
		{
			Write(UserFile, UserDataDocument.From(data));
		}
	}

	/// <inheritdoc />
	/// <remarks>On first use, the default categories are created from the seed and saved.</remarks>
	public IList<Category> LoadCategories(IReadOnlyDictionary<string, string[]> seedKeywords)
	{
		var categories = Read<List<CategoryDocument>>(CategoriesFile);
		if (categories is { Count: > 0 })
		{
			return [.. categories.Where(d => d is { HasId: true }).Select(d => d.ToDomain())];
		}

		var created = CategoryDefaults.Create(seedKeywords);
		Write(CategoriesFile, created.Select(CategoryDocument.From).ToList());
		return created;
	}

	/// <inheritdoc />
	public void SaveCategories(IEnumerable<Category> categories)
	{
		lock (_lock)
		{
			Write(CategoriesFile, categories.Select(CategoryDocument.From).ToList());
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Copies the transaction file to today's backup (once a day) and keeps the 30 most recent backups.
	/// </summary>
	private void Backup()
	{
		Directory.CreateDirectory(BackupDir);
		var target = Path.Combine(BackupDir, string.Create(CultureInfo.InvariantCulture, $"transactions-{DateTime.Today:yyyy-MM-dd}.json"));
		if (!File.Exists(target))
		{
			File.Copy(TxnFile, target);
		}

		foreach (var old in Directory.GetFiles(BackupDir, "transactions-*.json").OrderDescending(StringComparer.Ordinal).Skip(30))
		{
			File.Delete(old);
		}
	}

	/// <summary>
	/// Reads a JSON file, or returns <see langword="null"/> when it is missing or not valid JSON.
	/// </summary>
	private static T? Read<T>(string path)
		where T : class
	{
		if (!File.Exists(path))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
		}
		catch (JsonException)
		{
			// A corrupt file reads as empty rather than stopping the app; the daily backup keeps the previous content.
			return null;
		}
	}

	/// <summary>
	/// Writes a JSON file through a temporary file that then replaces it, so a crash never leaves it half written.
	/// </summary>
	private static void Write<T>(string path, T value)
	{
		var temporary = path + ".tmp";
		File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json));
		File.Move(temporary, path, overwrite: true);
	}

	/// <summary>
	/// Quotes a CSV field, doubling the quotes inside it.
	/// </summary>
	private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

	/// <summary>
	/// Logs that data from an old version was copied into the data folder.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Information, Message = "Moved existing data from {Old} to {New}")]
	private static partial void LogMovedData(ILogger logger, string old, string @new);

	/// <summary>
	/// Logs the data folder in use.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Information, Message = "Data folder: {Dir}")]
	private static partial void LogDataFolder(ILogger logger, string dir);
	#endregion
}
