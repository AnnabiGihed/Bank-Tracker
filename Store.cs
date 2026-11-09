using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KbcTracker;

public sealed record Txn(
    string Id,
    string AccountUid,
    DateOnly Date,
    decimal Amount,          // negative = money out, positive = money in
    string Currency,
    string Counterparty,
    string Description,
    string? CounterpartyIban = null,   // IBAN of the other side when the bank provides it (best key for loans/rent)
    string? BankCode = null,           // bank_transaction_code description, e.g. "Loan repayment"
    string? Raw = null);               // original JSON from the bank, for troubleshooting

public sealed class SessionInfo
{
    public string SessionId { get; set; } = "";
    public List<string> AccountUids { get; set; } = [];
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public DateTimeOffset? LastSync { get; set; }
}

/// <summary>Very small JSON-file store in ./data (keeps everything on your machine).</summary>
public sealed class Store
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "data");
    private string SessionFile => Path.Combine(_dir, "session.json");
    private string TxnFile => Path.Combine(_dir, "transactions.json");

    public Store() => Directory.CreateDirectory(_dir);

    public SessionInfo? LoadSession() =>
        File.Exists(SessionFile) ? JsonSerializer.Deserialize<SessionInfo>(File.ReadAllText(SessionFile)) : null;

    public void SaveSession(SessionInfo s) => File.WriteAllText(SessionFile, JsonSerializer.Serialize(s, Json));

    public List<Txn> LoadTransactions() =>
        File.Exists(TxnFile) ? JsonSerializer.Deserialize<List<Txn>>(File.ReadAllText(TxnFile)) ?? [] : [];

    /// <summary>Merges new transactions with what we already have (dedup by id), so history keeps growing past the bank's window.</summary>
    public int Merge(IEnumerable<Txn> incoming)
    {
        var map = LoadTransactions().ToDictionary(t => t.Id);
        var before = map.Count;
        foreach (var t in incoming) map[t.Id] = t;
        File.WriteAllText(TxnFile, JsonSerializer.Serialize(map.Values.OrderByDescending(t => t.Date).ToList(), Json));
        return map.Count - before;
    }

    // ---------- your corrections (recurring yes/no, category, name), kept in data/user-rules.json ----------
    private string RulesFile => Path.Combine(_dir, "user-rules.json");

    public UserRules LoadUserRules() =>
        File.Exists(RulesFile) ? JsonSerializer.Deserialize<UserRules>(File.ReadAllText(RulesFile)) ?? new() : new();

    public void SaveUserRules(UserRules r) => File.WriteAllText(RulesFile, JsonSerializer.Serialize(r, Json));

    public static string ToCsv(Analysis a)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Date;Amount;Currency;Name;Category;Fixed;Kind;Description\n");
        foreach (var i in a.Items.OrderBy(i => i.T.Date))
            sb.Append($"{i.T.Date:yyyy-MM-dd};{i.T.Amount.ToString(CultureInfo.InvariantCulture)};{i.T.Currency};{Q(i.Name)};{Q(i.Category)};{(i.IsRecurring ? "yes" : "")};{i.Kind};{Q(i.T.Description)}\n");
        return sb.ToString();
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
        // extra free-text fields some banks fill instead of remittance_information
        var extra = string.Join(" ", new[] { S("note"), S("additional_information"), S("remittance_information_unstructured"), S("reference_number") }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var description = string.Join(" ", new[] { remittance, extra }.Where(x => x.Length > 0)).Trim();

        var bankCode = S("bank_transaction_code", "description") ?? S("proprietary_bank_transaction_code", "description");
        var iban = amount < 0
            ? S("creditor_account", "iban") ?? S("creditor_account", "other", "identification")
            : S("debtor_account", "iban") ?? S("debtor_account", "other", "identification");

        var counterparty = amount < 0
            ? S("creditor", "name") ?? S("creditor_name")
            : S("debtor", "name") ?? S("debtor_name");
        if (string.IsNullOrWhiteSpace(counterparty))
            counterparty = description.Length > 0 ? description[..Math.Min(description.Length, 45)]
                         : bankCode ?? iban ?? "(unknown)";

        var id = S("entry_reference") ?? S("transaction_id")
                 ?? Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(accountUid + t.ToJsonString())));

        return new Txn(id, accountUid, date, amount, S("transaction_amount", "currency") ?? "EUR",
            counterparty.Trim(), description, iban?.Replace(" ", ""), bankCode, t.ToJsonString());
    }
}

public static class Categories
{
    public static Dictionary<string, string[]> Rules { get; set; } = [];
    public static List<FixedRule> FixedRules { get; set; } = [];
}

/// <summary>A payment you declare as fixed yourself (appsettings.json → "FixedPayments").</summary>
public sealed class FixedRule
{
    public string Name { get; set; } = "";
    /// <summary>Text to look for in payee, description, IBAN or bank code (case-insensitive). Several alternatives: "LENING|PRET|LOAN".</summary>
    public string Match { get; set; } = "";
    /// <summary>Optional: expected amount (positive). Used when no payment found yet and to tolerate small differences.</summary>
    public decimal? Amount { get; set; }
    /// <summary>Optional: expected day of month.</summary>
    public int? Day { get; set; }
}

/// <summary>Corrections you make in the UI. Keys are "series keys" (one per contract / shop / IBAN).</summary>
public sealed class UserRules
{
    public HashSet<string> Recurring { get; set; } = [];
    public HashSet<string> NotRecurring { get; set; } = [];
    public Dictionary<string, string> Categories { get; set; } = [];
    public Dictionary<string, string> Names { get; set; } = [];
}
