using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace KbcTracker;

/// <summary>
/// JSON files in a data folder that survives rebuilds (default %LOCALAPPDATA%\KbcTracker).
/// Transactions are only ever added or updated, never removed, so history keeps growing past KBC's 90-day window.
/// </summary>
public sealed class Store
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly object _lock = new();
    public string Dir { get; }

    private string SessionFile => Path.Combine(Dir, "session.json");
    private string TxnFile => Path.Combine(Dir, "transactions.json");
    private string UserFile => Path.Combine(Dir, "userdata.json");
    private string CategoriesFile => Path.Combine(Dir, "categories.json");
    private string BackupDir => Path.Combine(Dir, "backups");

    public Store(IConfiguration config, ILogger<Store> log)
    {
        var configured = config["DataDirectory"];
        Dir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KbcTracker")
            : Environment.ExpandEnvironmentVariables(configured);
        Directory.CreateDirectory(Dir);

        // Earlier versions kept data next to the binaries (bin/…/data) — move it once so a rebuild can't wipe it.
        var old = Path.Combine(AppContext.BaseDirectory, "data");
        if (!File.Exists(TxnFile) && Directory.Exists(old) && !string.Equals(Path.GetFullPath(old), Path.GetFullPath(Dir), StringComparison.OrdinalIgnoreCase))
        {
            foreach (var f in Directory.GetFiles(old, "*.json"))
                File.Copy(f, Path.Combine(Dir, Path.GetFileName(f)), overwrite: false);
            log.LogInformation("Moved existing data from {Old} to {New}", old, Dir);
        }
        log.LogInformation("Data folder: {Dir}", Dir);
    }

    // ---------- session ----------
    public SessionInfo? LoadSession() => Read<SessionInfo>(SessionFile);
    public void SaveSession(SessionInfo s) => Write(SessionFile, s);

    // ---------- transactions ----------
    public List<Txn> LoadTransactions() => Read<List<Txn>>(TxnFile) ?? [];

    /// <summary>Adds new / updates known transactions (dedup by id). Never deletes. Keeps a daily backup.</summary>
    public int Merge(IEnumerable<Txn> incoming)
    {
        lock (_lock)
        {
            var map = LoadTransactions().ToDictionary(t => t.Id);
            var before = map.Count;
            foreach (var t in incoming) map[t.Id] = t;
            if (File.Exists(TxnFile)) Backup();
            Write(TxnFile, map.Values.OrderByDescending(t => t.Date).ToList());
            return map.Count - before;
        }
    }

    private void Backup()
    {
        Directory.CreateDirectory(BackupDir);
        var target = Path.Combine(BackupDir, $"transactions-{DateTime.Today:yyyy-MM-dd}.json");
        if (!File.Exists(target)) File.Copy(TxnFile, target);
        foreach (var old in Directory.GetFiles(BackupDir, "transactions-*.json").OrderDescending().Skip(30))
            File.Delete(old);
    }

    // ---------- your decisions ----------
    public UserData LoadUserData() => Read<UserData>(UserFile) ?? new();
    public void SaveUserData(UserData d) { lock (_lock) Write(UserFile, d); }

    // ---------- categories ----------
    public List<Category> LoadCategories(Dictionary<string, string[]> seedKeywords)
    {
        var cats = Read<List<Category>>(CategoriesFile);
        if (cats is { Count: > 0 }) return cats;
        cats = DefaultCategories(seedKeywords);
        Write(CategoriesFile, cats);
        return cats;
    }

    public void SaveCategories(List<Category> c) { lock (_lock) Write(CategoriesFile, c); }

    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        return sb.ToString().Trim('-');
    }

    private static readonly string[] Palette =
        ["#3454d1", "#f2994a", "#27ae60", "#9b51e0", "#eb5757", "#2d9cdb", "#f2c94c", "#6fcf97", "#bb6bd9", "#56ccf2", "#219653", "#e67e22", "#16a085", "#c0392b", "#8e44ad"];

    /// <summary>First-run categories: built-in ones + the keyword lists from appsettings.json.</summary>
    private static List<Category> DefaultCategories(Dictionary<string, string[]> seed)
    {
        var list = new List<Category>
        {
            new() { Id = "income", Name = "Income", Type = CategoryType.Income, Color = "#27ae60", BuiltIn = true },
            new() { Id = "refunds", Name = "Refunds", Type = CategoryType.Income, Color = "#6fcf97", BuiltIn = true },
            new() { Id = "savings", Name = Analysis.SavingsName, Type = CategoryType.Savings, Color = "#9b51e0", BuiltIn = true },
            new() { Id = "own-transfers", Name = Analysis.OwnTransfersName, Type = CategoryType.Transfer, Color = "#bdbdbd", BuiltIn = true },
            new() { Id = "loan", Name = "Loan", Type = CategoryType.Expense, Color = "#c0392b", BuiltIn = true },
            new() { Id = "bills", Name = "Bills & direct debits", Type = CategoryType.Expense, Color = "#2d9cdb", BuiltIn = true },
            new() { Id = "bank-fees", Name = "Bank & fees", Type = CategoryType.Expense, Color = "#828282", BuiltIn = true },
            new() { Id = "cash", Name = "Cash", Type = CategoryType.Expense, Color = "#f2c94c", BuiltIn = true },
            new() { Id = "transfers", Name = "Transfers to others", Type = CategoryType.Expense, Color = "#56ccf2", BuiltIn = true },
            new() { Id = "other", Name = "Other", Type = CategoryType.Expense, Color = "#a0a7b4", BuiltIn = true },
        };
        var i = 0;
        foreach (var (name, words) in seed)
        {
            var id = Slug(name);
            var existing = list.FirstOrDefault(c => c.Id == id || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { existing.Keywords.AddRange(words); continue; }
            list.Add(new Category
            {
                Id = id, Name = name, Keywords = [.. words], Color = Palette[i++ % Palette.Length],
                Type = name.Equals(Analysis.OwnTransfersName, StringComparison.OrdinalIgnoreCase) ? CategoryType.Transfer : CategoryType.Expense
            });
        }
        return list;
    }

    // ---------- CSV ----------
    public static string ToCsv(Analysis a)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description\n");
        foreach (var i in a.Items.OrderBy(i => i.T.Date))
            sb.Append($"{i.T.Date:yyyy-MM-dd};{i.T.Amount.ToString(CultureInfo.InvariantCulture)};{i.T.Currency};{Q(i.Name)};{Q(i.Category.Name)};{(i.CategoryConfirmed ? "yes" : "")};{Q(i.Recurring?.Name ?? "")};{Q(i.T.Description)}\n");
        return sb.ToString();
    }

    // ---------- helpers ----------
    private static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Write to a temp file then replace, so a crash never leaves a half-written file.</summary>
    private static void Write<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
        File.Move(tmp, path, overwrite: true);
    }

    // ---------- Mapping from Enable Banking's transaction JSON ----------
    public static Txn Map(string accountUid, JsonNode t)
    {
        string? S(params string[] path)
        {
            JsonNode? n = t;
            foreach (var p in path) n = n?[p];
            return n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        }

        var amount = decimal.Parse(S("transaction_amount", "amount") ?? "0", CultureInfo.InvariantCulture);
        if (S("credit_debit_indicator") == "DBIT") amount = -Math.Abs(amount);
        var date = DateOnly.Parse(S("booking_date") ?? S("value_date") ?? S("transaction_date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"), CultureInfo.InvariantCulture);

        var remittance = t["remittance_information"] is JsonArray arr
            ? string.Join(" ", arr.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)))
            : S("remittance_information") ?? "";
        var extra = string.Join(" ", new[] { S("note"), S("additional_information"), S("reference_number") }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var description = string.Join(" ", new[] { remittance, extra }.Where(x => x.Length > 0)).Trim();

        var bankCode = S("bank_transaction_code", "description");
        var iban = amount < 0 ? S("creditor_account", "iban") : S("debtor_account", "iban");
        var counterparty = amount < 0 ? S("creditor", "name") : S("debtor", "name");
        if (string.IsNullOrWhiteSpace(counterparty))
            counterparty = description.Length > 0 ? description[..Math.Min(description.Length, 45)] : bankCode ?? iban ?? "(unknown)";

        var id = S("entry_reference") ?? S("transaction_id")
                 ?? Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(accountUid + t.ToJsonString())));

        return new Txn(id, accountUid, date, amount, S("transaction_amount", "currency") ?? "EUR",
            counterparty.Trim(), description, iban?.Replace(" ", ""), bankCode, t.ToJsonString());
    }
}

/// <summary>Keyword lists from appsettings.json, used only to create categories.json the first time.</summary>
public static class Categories
{
    public static Dictionary<string, string[]> Seed { get; set; } = [];
}
