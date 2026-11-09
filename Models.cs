namespace KbcTracker;

public sealed record Txn(
    string Id,
    string AccountUid,
    DateOnly Date,
    decimal Amount,          // negative = money out, positive = money in
    string Currency,
    string Counterparty,
    string Description,
    string? CounterpartyIban = null,
    string? BankCode = null,
    string? Raw = null);     // original JSON from the bank

public sealed class SessionInfo
{
    public string SessionId { get; set; } = "";
    public List<string> AccountUids { get; set; } = [];
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public DateTimeOffset? LastSync { get; set; }
    public string? LastError { get; set; }
}

/// <summary>How a category counts in the totals.</summary>
public enum CategoryType
{
    Expense,    // counted as spending
    Income,     // counted as income
    Savings,    // money put aside (shown separately, not spending)
    Transfer    // moves between your own accounts (ignored in totals)
}

public sealed class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public CategoryType Type { get; set; } = CategoryType.Expense;
    public string Color { get; set; } = "#828282";
    /// <summary>Words looked for in the payee/shop name (and description for non-card payments). Longest match wins.</summary>
    public List<string> Keywords { get; set; } = [];
    public bool BuiltIn { get; set; }
}

/// <summary>A fixed payment you accepted. New transactions are matched to it automatically.</summary>
public sealed class RecurringPayment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Name { get; set; } = "";
    public string? CategoryId { get; set; }
    /// <summary>Payee series it covers (direct-debit mandate, IBAN, shop…).</summary>
    public List<string> SeriesKeys { get; set; } = [];
    /// <summary>Shop-name words that identify it even if the bank label changes (e.g. GOOGLE YOUTUBE).</summary>
    public List<string> Tokens { get; set; } = [];
    public decimal Amount { get; set; }
    /// <summary>Allowed difference with <see cref="Amount"/> for token matching, in % (energy bills vary more than Netflix).</summary>
    public decimal TolerancePct { get; set; } = 15;
    public int CadenceDays { get; set; } = 30;
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PayeeAssignment
{
    public string? CategoryId { get; set; }
    public bool Confirmed { get; set; }
}

/// <summary>Everything you decided in the UI. Saved in data/userdata.json.</summary>
public sealed class UserData
{
    public List<RecurringPayment> Recurring { get; set; } = [];
    /// <summary>Suggestions you rejected (signature = their series keys).</summary>
    public HashSet<string> DismissedSuggestions { get; set; } = [];
    /// <summary>Category per payee series (applies to all its transactions).</summary>
    public Dictionary<string, PayeeAssignment> Payees { get; set; } = [];
    /// <summary>Category for one single transaction (overrides the payee).</summary>
    public Dictionary<string, string> TxnCategories { get; set; } = [];
    public Dictionary<string, string> PayeeNames { get; set; } = [];
}
