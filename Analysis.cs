using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KbcTracker;

/// <summary>What kind of movement a transaction is, from the ISO bank transaction code KBC sends.</summary>
public enum Kind
{
    Card, DirectDebit, Loan, SavingsPlan, BankFee, Transfer, OwnTransferOut, SpareChange, CashWithdrawal,
    Income, OwnTransferIn, LoanReceived, CashDeposit, Refund, Other
}

/// <summary>A transaction with its payee series, category and fixed payment.</summary>
public sealed record TxnInfo(Txn T, Kind Kind, string SeriesKey, string Name, string[] Tokens, Category Category, bool CategoryConfirmed, RecurringPayment? Recurring)
{
    public bool IsSpending => Category.Type == CategoryType.Expense && T.Amount < 0;
    public bool IsIncome => Category.Type == CategoryType.Income && T.Amount > 0;
    public bool IsSaving => Category.Type == CategoryType.Savings;
    public bool IsOwnMove => Category.Type == CategoryType.Transfer;
    public string Month => $"{T.Date:yyyy-MM}";
}

/// <summary>A possible fixed payment found in the history, waiting for you to accept or dismiss it.</summary>
public sealed record Suggestion(string Signature, string Name, string CategoryId, decimal Amount, int CadenceDays,
    bool HighConfidence, string Reason, List<TxnInfo> Items, List<string> SeriesKeys, List<string> Tokens, Kind Kind)
{
    public string Cadence => Analysis.CadenceName(CadenceDays);
}

public enum FixedStatus { Paid, PaidChanged, Due, Late, NotThisMonth, NotStarted }

public sealed record FixedRow(RecurringPayment Def, Category Category, List<TxnInfo> Payments)
{
    public DateOnly? LastPaid => Payments.Count == 0 ? null : Payments.Max(p => p.T.Date);
    public int UsualDay => Payments.Count == 0 ? 1 : Payments.Select(p => p.T.Date.Day).OrderBy(d => d).ElementAt(Payments.Count / 2);

    public (FixedStatus Status, decimal Paid, DateOnly? Date) StatusIn(string month, DateOnly today)
    {
        var inMonth = Payments.Where(p => p.Month == month).ToList();
        if (inMonth.Count > 0)
        {
            var paid = inMonth.Sum(p => -p.T.Amount);
            var changed = Math.Abs(paid - Def.Amount) >= 1 && Math.Abs(paid - Def.Amount) > Def.Amount * 0.02m;
            return (changed ? FixedStatus.PaidChanged : FixedStatus.Paid, paid, inMonth.Max(p => p.T.Date));
        }
        var first = Payments.Count == 0 ? DateOnly.FromDateTime(Def.CreatedAt.LocalDateTime) : Payments.Min(p => p.T.Date);
        if (string.CompareOrdinal(month, $"{first:yyyy-MM}") < 0) return (FixedStatus.NotStarted, 0, null);

        // project the next payment dates from the last one, using the cadence
        var last = LastPaid ?? first;
        var expected = Enumerable.Range(1, 24).Select(k => last.AddDays(k * Def.CadenceDays)).Where(d => $"{d:yyyy-MM}" == month).ToList();
        if (expected.Count == 0 && Def.CadenceDays > 40) return (FixedStatus.NotThisMonth, 0, null);
        var exp = expected.FirstOrDefault(new DateOnly(int.Parse(month[..4]), int.Parse(month[5..]), Math.Min(UsualDay, 28)));
        if (string.CompareOrdinal(month, $"{today:yyyy-MM}") < 0) return (FixedStatus.NotThisMonth, 0, exp);
        return (today <= exp.AddDays(4) ? FixedStatus.Due : FixedStatus.Late, 0, exp);
    }
}

public sealed record MonthSummary(string Month, decimal Income, decimal Fixed, decimal Variable, decimal Saved, Dictionary<string, decimal> ByCategory)
{
    public decimal Spent => Fixed + Variable;
    public decimal Net => Income - Spent - Saved;
}

/// <summary>A payee whose automatic category you haven't confirmed yet.</summary>
public sealed record ReviewItem(string SeriesKey, string Name, Category Category, int Count, decimal Total, DateOnly Last, bool Incoming, string Sample);

public sealed partial class Analysis
{
    public const string OwnTransfersName = "Own transfers";
    public const string SavingsName = "Savings & investments";

    public List<TxnInfo> Items { get; }
    public List<FixedRow> Fixed { get; }
    public List<Suggestion> Suggestions { get; }
    public List<ReviewItem> Review { get; }
    public List<MonthSummary> Months { get; }
    public List<string> AvailableMonths { get; }
    public List<Category> CategoryList { get; }
    public string HolderName { get; }

    private readonly Dictionary<string, Category> _cats;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public Analysis(List<Txn> txns, List<Category> categories, UserData user)
    {
        CategoryList = categories;
        _cats = categories.ToDictionary(c => c.Id);
        var other = _cats.GetValueOrDefault("other") ?? new Category { Id = "other", Name = "Other" };
        Category Cat(string? id) => id is not null && _cats.TryGetValue(id, out var c) ? c : other;

        var parsed = txns.Select(t => (t, raw: Raw.From(t))).ToList();
        HolderName = parsed.Where(p => p.t.Amount < 0).Select(p => p.raw.DebtorName).Where(n => !string.IsNullOrWhiteSpace(n))
                           .GroupBy(n => Norm(n!)).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";

        var active = user.Recurring.Where(r => r.Active).ToList();
        Items = parsed.Select(p =>
        {
            var (kind, key, name, tokens, autoCat) = Classify(p.t, p.raw);
            name = user.PayeeNames.GetValueOrDefault(key, name);
            var rec = p.t.Amount < 0 ? Match(active, key, tokens, -p.t.Amount) : null;

            // category: this transaction > this payee > the fixed payment > automatic
            string catId; bool confirmed;
            if (user.TxnCategories.TryGetValue(p.t.Id, out var tc)) (catId, confirmed) = (tc, true);
            else if (user.Payees.TryGetValue(key, out var pa) && pa.CategoryId is not null) (catId, confirmed) = (pa.CategoryId, pa.Confirmed);
            else if (rec?.CategoryId is not null) (catId, confirmed) = (rec.CategoryId, true);
            // categories the bank code makes certain (loan, own transfer, fee…) don't need your confirmation
            else (catId, confirmed) = (autoCat, kind is Kind.Loan or Kind.SavingsPlan or Kind.SpareChange or Kind.BankFee or Kind.OwnTransferOut
                                                 or Kind.OwnTransferIn or Kind.LoanReceived or Kind.CashDeposit or Kind.CashWithdrawal);

            return new TxnInfo(p.t, kind, key, rec?.Name ?? name, tokens, Cat(catId), confirmed, rec);
        }).OrderByDescending(i => i.T.Date).ThenBy(i => i.T.Amount).ToList();

        Fixed = active.Select(r => new FixedRow(r, Cat(r.CategoryId ?? Items.FirstOrDefault(i => i.Recurring == r)?.Category.Id),
                                               Items.Where(i => i.Recurring == r).OrderBy(i => i.T.Date).ToList()))
                      .OrderBy(f => f.UsualDay).ToList();
        Suggestions = Suggest(user);
        Review = Items.Where(i => !i.CategoryConfirmed).GroupBy(i => i.SeriesKey)
                      .Select(g => new ReviewItem(g.Key, g.First().Name, g.First().Category, g.Count(), g.Sum(i => i.T.Amount),
                                                  g.Max(i => i.T.Date), g.First().T.Amount > 0, g.First().T.Description))
                      .OrderByDescending(r => Math.Abs(r.Total)).ToList();

        AvailableMonths = Items.Select(i => i.Month).Distinct().OrderDescending().ToList();
        if (!AvailableMonths.Contains($"{_today:yyyy-MM}")) AvailableMonths.Insert(0, $"{_today:yyyy-MM}");
        Months = AvailableMonths.OrderBy(m => m).Select(BuildMonth).ToList();
    }

    public MonthSummary MonthData(string month) => Months.FirstOrDefault(m => m.Month == month) ?? new(month, 0, 0, 0, 0, []);
    public Category CategoryById(string id) => _cats.GetValueOrDefault(id) ?? _cats["other"];

    private static RecurringPayment? Match(List<RecurringPayment> defs, string key, string[] tokens, decimal amount)
    {
        foreach (var d in defs) if (d.SeriesKeys.Contains(key)) return d;
        foreach (var d in defs)
            if (d.Tokens.Count > 0 && d.Tokens.All(tokens.Contains) && Math.Abs(amount - d.Amount) <= Math.Max(0.5m, d.Amount * d.TolerancePct / 100))
                return d;
        return null;
    }

    // ================================================================== classification

    private (Kind, string Key, string Name, string[] Tokens, string CategoryId) Classify(Txn t, Raw r)
    {
        var debit = t.Amount < 0;
        var other = debit ? r.CreditorName : r.DebtorName;
        var otherIban = debit ? r.CreditorIban : r.DebtorIban;
        var own = HolderName.Length > 0 && other is not null && Norm(other) == HolderName;
        var desc = t.Description;

        var kind = (r.Code, r.SubCode, debit) switch
        {
            ("CSLN", _, true) => Kind.Loan,
            ("IDDT", _, true) => Kind.DirectDebit,
            ("SETT", "SUBS", true) => Kind.SavingsPlan,
            ("OPCL", _, true) or ("ACMT", _, true) => Kind.BankFee,
            ("ICCN", _, true) when desc.Contains("spare change", StringComparison.OrdinalIgnoreCase) => Kind.SpareChange,
            ("CCRD", "CWDL", true) => Kind.CashWithdrawal,
            ("CCRD", "CDPT", false) => Kind.CashDeposit,
            ("CCRD", _, true) => Kind.Card,
            ("CCRD", _, false) => Kind.Refund,
            ("RCDT", "CAJT", false) => Kind.LoanReceived,
            (_, _, true) when own => Kind.OwnTransferOut,
            (_, _, false) when own => Kind.OwnTransferIn,
            (_, _, true) when r.Code is "ICDT" or "IRCT" or "ICCN" or "RCDT" or "RRCT" => Kind.Transfer,
            (_, _, false) => Kind.Income,
            _ => Kind.Other
        };
        if (r.Code is null && debit)
        {
            if (desc.Contains("Direct debit", StringComparison.OrdinalIgnoreCase) || desc.Contains("Domicili", StringComparison.OrdinalIgnoreCase)) kind = Kind.DirectDebit;
            else if (PaymentWith().IsMatch(desc)) kind = Kind.Card;
        }

        string name, key;
        switch (kind)
        {
            case Kind.Card or Kind.Refund:
                name = Merchant(desc.Length > 0 ? desc : t.Counterparty);
                key = "CARD:" + string.Join(' ', Tokens(name).Take(3).DefaultIfEmpty(Norm(name)));
                break;
            case Kind.DirectDebit:
                name = Title(other ?? t.Counterparty);
                var mandate = Mandate().Match(desc);
                key = "DD:" + Norm(name) + (mandate.Success ? ":" + mandate.Groups[1].Value : "");
                break;
            case Kind.Loan:
                name = "Loan repayment";
                var reference = LoanRef().Match(desc);
                key = "LOAN:" + (reference.Success ? reference.Groups[1].Value : "1");
                break;
            case Kind.SavingsPlan:
                name = Title(Before(desc, " Subscription"));
                key = "PLAN:" + Norm(name);
                break;
            case Kind.BankFee:
                name = Title(Before(Before(desc, " Period"), " Periode").Replace("Charge ", "", StringComparison.OrdinalIgnoreCase)) + " (fee)";
                key = "FEE:" + Norm(name);
                break;
            case Kind.SpareChange:
                (name, key) = ("Spare change investing", "SPARE");
                break;
            case Kind.CashWithdrawal or Kind.CashDeposit:
                (name, key) = kind == Kind.CashDeposit ? ("Cash deposit", "CASHIN") : ("Cash withdrawal", "CASHOUT");
                break;
            case Kind.LoanReceived:
                (name, key) = ("Loan received", "LOANIN");
                break;
            default:
                name = Title(other ?? t.Counterparty);
                key = (debit ? "TO:" : "FROM:") + (otherIban ?? Norm(name));
                break;
        }
        var tokens = Tokens(name);

        var catId = kind switch
        {
            Kind.Loan => "loan",
            Kind.SavingsPlan or Kind.SpareChange => "savings",
            Kind.BankFee => "bank-fees",
            Kind.OwnTransferOut or Kind.OwnTransferIn or Kind.LoanReceived or Kind.CashDeposit => "own-transfers",
            Kind.CashWithdrawal => "cash",
            Kind.Income => KeywordCategory(name, CategoryType.Income) ?? "income",
            Kind.Refund => "refunds",
            // card texts contain "Google Pay", "KBC", "Mastercard"… → only the shop name counts
            _ => KeywordCategory(name, null) ?? (kind == Kind.Card ? null : KeywordCategory(desc, null))
                 ?? kind switch { Kind.DirectDebit => "bills", Kind.Transfer => "transfers", _ => "other" }
        };
        return (kind, key, name, tokens, catId);
    }

    /// <summary>Category whose keyword matches; the longest keyword wins ("AUTOMOBILE" beats "CARREFOUR").</summary>
    private string? KeywordCategory(string text, CategoryType? only)
    {
        var u = " " + text.ToUpperInvariant() + " ";
        return CategoryList.Where(c => only is null || c.Type == only)
            .SelectMany(c => c.Keywords.Select(w => (c.Id, w: w.ToUpperInvariant())))
            .Where(x => x.w.Trim().Length > 0 && u.Contains(x.w))
            .OrderByDescending(x => x.w.Trim().Length)
            .Select(x => x.Id).FirstOrDefault();
    }

    // ================================================================== recurring suggestions

    /// <summary>
    /// Groups payments that are probably the same thing (same payee series, or same amount and a shared
    /// distinctive shop word, e.g. three different "Google YouTube" labels), then keeps groups that come back
    /// on a regular rhythm (monthly / quarterly / yearly) with a stable amount.
    /// </summary>
    private List<Suggestion> Suggest(UserData user)
    {
        var pool = Items.Where(i => i.T.Amount < 0 && i.Recurring is null
                                    && i.Kind is not (Kind.OwnTransferOut or Kind.SpareChange or Kind.CashWithdrawal)
                                    && i.Category.Type != CategoryType.Transfer).ToList();

        // union-find over transactions
        var parent = Enumerable.Range(0, pool.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int b) => parent[Find(a)] = Find(b);

        foreach (var g in pool.Select((t, i) => (t, i)).GroupBy(x => x.t.SeriesKey))
            foreach (var x in g.Skip(1)) Union(g.First().i, x.i);

        // fuzzy link: similar amount (±3%) + a distinctive word in common (≥5 letters), same broad kind
        var byAmount = pool.Select((t, i) => (t, i)).OrderBy(x => -x.t.T.Amount).ToList();
        for (var a = 0; a < byAmount.Count; a++)
        {
            var (ta, ia) = byAmount[a];
            var amtA = -ta.T.Amount;
            for (var b = a + 1; b < byAmount.Count; b++)
            {
                var (tb, ib) = byAmount[b];
                if (-tb.T.Amount > amtA * 1.03m + 0.01m) break;
                if (ta.SeriesKey == tb.SeriesKey || IsContract(ta.Kind) || IsContract(tb.Kind)) continue;
                if (ta.Tokens.Any(w => w.Length >= 5 && tb.Tokens.Contains(w))) Union(ia, ib);
            }
        }

        var result = new List<Suggestion>();
        foreach (var cluster in pool.Select((t, i) => (t, i)).GroupBy(x => Find(x.i)).Select(g => g.Select(x => x.t).OrderBy(t => t.T.Date).ToList()))
        {
            var keys = cluster.Select(c => c.SeriesKey).Distinct().OrderBy(k => k).ToList();
            var signature = string.Join("|", keys);
            if (user.DismissedSuggestions.Contains(signature)) continue;
            var s = Evaluate(cluster, keys, signature);
            if (s is not null) result.Add(s);
        }
        return result.OrderByDescending(s => s.HighConfidence).ThenByDescending(s => s.Amount).ToList();
    }

    private static bool IsContract(Kind k) => k is Kind.DirectDebit or Kind.Loan or Kind.SavingsPlan or Kind.BankFee;

    private Suggestion? Evaluate(List<TxnInfo> c, List<string> keys, string signature)
    {
        var kind = c.GroupBy(x => x.Kind).OrderByDescending(g => g.Count()).First().Key;
        var amounts = c.Select(x => -x.T.Amount).OrderBy(a => a).ToList();
        var median = amounts[amounts.Count / 2];
        if (median < 1) return null;

        var months = c.Select(x => x.Month).Distinct().Count();
        var intervals = c.Zip(c.Skip(1), (a, b) => b.T.Date.DayNumber - a.T.Date.DayNumber).Where(d => d > 0).ToList();
        var medInterval = intervals.Count == 0 ? 0 : intervals.OrderBy(d => d).ElementAt(intervals.Count / 2);
        int? cadence = medInterval switch { >= 26 and <= 35 => 30, >= 84 and <= 98 => 91, >= 350 and <= 380 => 365, _ => null };

        string reason; bool high;
        if (IsContract(kind))
        {
            // the bank says it is a mandate / loan / plan / fee → regular months are enough
            if (months < 2 || (double)c.Count / months > 1.5) return null;
            cadence ??= 30;
            high = true;
            reason = kind switch { Kind.DirectDebit => "direct debit", Kind.Loan => "loan instalment", Kind.SavingsPlan => "savings plan", _ => "bank fee" }
                     + $" · {c.Count} payments";
        }
        else
        {
            if (cadence is null || c.Count < 2) return null;
            var tol = cadence == 30 ? 7 : 12;
            if (intervals.Any(d => Math.Abs(d - cadence.Value) > tol)) return null;          // must be regular
            var spread = kind == Kind.Card ? 0.10m : 0.15m;
            if (amounts.Any(a => Math.Abs(a - median) > median * spread)) return null;      // stable amount
            if (c.Count == 2 && amounts[^1] - amounts[0] > Math.Max(0.05m, median * 0.01m)) return null;
            high = c.Count >= 3;
            reason = $"{(kind == Kind.Card ? "card payment" : "transfer")} · {c.Count}× {CadenceName(cadence.Value).ToLowerInvariant()}, same amount";
        }

        // ended? (last payment much older than the rhythm)
        if (_today.DayNumber - c[^1].T.Date.DayNumber > cadence!.Value * 2 + 5) return null;

        var names = c.GroupBy(x => x.Name).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(x => x.T.Date)).First().Key;
        var tokens = kind is Kind.Card or Kind.Refund
            ? c.Select(x => (IEnumerable<string>)x.Tokens).Aggregate((a, b) => a.Intersect(b)).ToList()
            : [];
        var category = c.GroupBy(x => x.Category.Id).OrderByDescending(g => g.Count()).First().Key;
        return new Suggestion(signature, names, category, median, cadence.Value, high, reason, c, keys, tokens, kind);
    }

    /// <summary>Turns a suggestion (or a hand-picked payee) into a fixed payment definition.</summary>
    public static RecurringPayment ToDefinition(Suggestion s) => new()
    {
        Name = s.Name,
        CategoryId = s.CategoryId,
        SeriesKeys = s.SeriesKeys,
        Tokens = s.Tokens,
        Amount = s.Amount,
        CadenceDays = s.CadenceDays,
        TolerancePct = s.Kind switch { Kind.DirectDebit or Kind.BankFee => 60, Kind.Card => 10, _ => 15 }
    };

    /// <summary>For "mark as fixed" on a transaction: builds a suggestion-like group from its payee.</summary>
    public Suggestion GroupFor(string seriesKey)
    {
        var items = Items.Where(i => i.SeriesKey == seriesKey && i.T.Amount < 0).OrderBy(i => i.T.Date).ToList();
        var sig = seriesKey;
        return Evaluate(items, [seriesKey], sig) ?? new Suggestion(sig, items[^1].Name, items[^1].Category.Id,
            items.Select(i => -i.T.Amount).OrderBy(a => a).ElementAt(items.Count / 2), 30, false, "marked by you",
            items, [seriesKey], (items[^1].Kind is Kind.Card ? items[^1].Tokens.ToList() : new List<string>()), items[^1].Kind);
    }

    public static string CadenceName(int days) => days switch { <= 35 => "Monthly", <= 100 => "Quarterly", _ => "Yearly" };

    private MonthSummary BuildMonth(string month)
    {
        var m = Items.Where(i => i.Month == month).ToList();
        var spend = m.Where(i => i.Category.Type == CategoryType.Expense).ToList();   // credits in an expense category (refunds) reduce spending
        return new MonthSummary(month,
            m.Where(i => i.IsIncome).Sum(i => i.T.Amount),
            spend.Where(i => i.Recurring is not null).Sum(i => -i.T.Amount),
            spend.Where(i => i.Recurring is null).Sum(i => -i.T.Amount),
            m.Where(i => i.IsSaving).Sum(i => -i.T.Amount),
            spend.GroupBy(i => i.Category.Id).ToDictionary(g => g.Key, g => g.Sum(i => -i.T.Amount)));
    }

    // ================================================================== text helpers

    public static string Norm(string s)
    {
        var words = NonLetters().Replace(s.ToUpperInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).Take(4);
        var r = string.Join(' ', words);
        return r.Length > 0 ? r : s.Trim().ToUpperInvariant();
    }

    private static readonly HashSet<string> Stop =
    [
        "THE", "AND", "ONLINE", "SHOP", "STORE", "PAYMENT", "BELGIUM", "BELGIQUE", "BELGIE", "BRUXELLES", "BRUSSEL", "BRUSSELS", "BRUX",
        "PARIS", "DUBLIN", "LUXEMBOURG", "AMSTERDAM", "LONDON", "IXELLES", "ELSENE", "ANDERLECHT", "ETTERBEEK", "SCHAERBEEK",
        "SPRL", "SRL", "BVBA", "SAS", "SARL", "LTD", "INC", "GMBH", "COM", "WWW", "NET", "SERVICES", "SERVICE"
    ];

    /// <summary>Distinctive words of a payee/shop name, used to recognise it when the bank label changes.</summary>
    public static string[] Tokens(string name) =>
        NonLetters().Replace(name.ToUpperInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !Stop.Contains(w)).Distinct().ToArray();

    private static readonly HashSet<string> Prefixes = ["WL", "SQ", "SUMUP", "SP", "ZETTLE", "ZTL", "IZ", "PP", "PAYPAL", "UBR", "CCV", "MOLLIE"];

    /// <summary>
    /// Shop name from a KBC card label: "WL*GOOGLE YouTube IED02 R296 Dublin Payment with…" → "Google Youtube".
    /// Stops at the country+postcode token (BE1000, IE2, IEUNKNOWN…), drops processor prefixes and numbers.
    /// </summary>
    public static string Merchant(string text)
    {
        var head = PaymentWith().Split(text)[0].Replace('*', ' ').Replace('_', ' ');
        var words = head.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var cut = words.Count > 1 ? words.FindIndex(1, w => CountryPostcode().IsMatch(w)) : -1;
        if (cut > 0) words = words.GetRange(0, cut);
        while (words.Count > 1 && (Prefixes.Contains(words[0].ToUpperInvariant()) || words[0].All(char.IsDigit))) words.RemoveAt(0);
        words = words.Where(w => !w.All(char.IsDigit)).ToList();
        var name = string.Join(' ', words);
        return Title(name.Length > 0 ? name : text);
    }

    private static string Before(string s, string marker)
    {
        var i = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return (i > 0 ? s[..i] : s).Trim();
    }

    private static string Title(string s)
    {
        s = s.Trim();
        if (s.Length > 40) s = s[..40].Trim();
        return s.Any(char.IsLower) ? s : CultureInfo.GetCultureInfo("fr-BE").TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    [GeneratedRegex(@"[^A-ZÀ-ſ ]")] private static partial Regex NonLetters();
    [GeneratedRegex(@"\s+(Payment with|Paiement avec|Betaling met|Payment via)\s", RegexOptions.IgnoreCase)] private static partial Regex PaymentWith();
    [GeneratedRegex(@"^[A-Za-z]{2}([A-Za-z]{0,2}\d+[A-Za-z0-9]*|UNKNOWN)$")] private static partial Regex CountryPostcode();
    [GeneratedRegex(@"(?:Direct debit|Domiciliation|Domiciliëring)\s+\d+\s+(\S+)", RegexOptions.IgnoreCase)] private static partial Regex Mandate();
    [GeneratedRegex(@"Reference\s+(\S+)", RegexOptions.IgnoreCase)] private static partial Regex LoanRef();
}

/// <summary>The few fields we need from the bank's raw JSON (kept in Txn.Raw).</summary>
public sealed record Raw(string? Code, string? SubCode, string? CreditorName, string? DebtorName, string? CreditorIban, string? DebtorIban)
{
    public static Raw From(Txn t)
    {
        if (string.IsNullOrEmpty(t.Raw)) return new(null, null, t.Amount < 0 ? t.Counterparty : null, t.Amount > 0 ? t.Counterparty : null, t.CounterpartyIban, null);
        var j = JsonNode.Parse(t.Raw)!;
        static string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;
        return new(
            S(j["bank_transaction_code"]?["code"]), S(j["bank_transaction_code"]?["sub_code"]),
            S(j["creditor"]?["name"]), S(j["debtor"]?["name"]),
            S(j["creditor_account"]?["iban"])?.Replace(" ", ""), S(j["debtor_account"]?["iban"])?.Replace(" ", ""));
    }
}
