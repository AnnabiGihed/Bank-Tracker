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
    string Description);

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

    public string ToCsv(IEnumerable<Txn> txns)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Date;Amount;Currency;Counterparty;Description;Category;Fixed\n");
        var analysis = new Analysis(txns.ToList(), Categories.Rules);
        foreach (var t in txns.OrderBy(t => t.Date))
            sb.Append($"{t.Date:yyyy-MM-dd};{t.Amount.ToString(CultureInfo.InvariantCulture)};{t.Currency};{Q(t.Counterparty)};{Q(t.Description)};{Q(analysis.CategoryOf(t))};{(analysis.IsFixed(t) ? "yes" : "")}\n");
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

        var counterparty = amount < 0
            ? S("creditor", "name") ?? S("creditor_name")
            : S("debtor", "name") ?? S("debtor_name");
        if (string.IsNullOrWhiteSpace(counterparty))
            counterparty = remittance.Length > 0 ? remittance[..Math.Min(remittance.Length, 45)] : "(unknown)";

        var id = S("entry_reference") ?? S("transaction_id")
                 ?? Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(accountUid + t.ToJsonString())));

        return new Txn(id, accountUid, date, amount, S("transaction_amount", "currency") ?? "EUR", counterparty.Trim(), remittance.Trim());
    }
}

public static class Categories
{
    public static Dictionary<string, string[]> Rules { get; set; } = [];
}
