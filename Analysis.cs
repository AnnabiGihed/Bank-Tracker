using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KbcTracker;

/// <summary>What kind of movement a transaction is, derived from the ISO bank transaction code KBC sends.</summary>
public enum Kind
{
    Card, DirectDebit, Loan, SavingsPlan, BankFee, Transfer, OwnTransferOut, SpareChange, CashWithdrawal,
    Income, OwnTransferIn, LoanReceived, CashDeposit, Refund, Other
}

/// <summary>A transaction enriched with everything the UI needs.</summary>
public sealed record TxnInfo(Txn T, Kind Kind, string SeriesKey, string Name, string Category, bool IsRecurring)
{
    // Category decides, so moving something to "Own transfers" / "Savings & investments" (in the UI or via keywords) takes it out of spending
    public bool IsOwnMove => Category == Analysis.OwnTransfers;
    public bool IsSaving => T.Amount < 0 && Category == Analysis.Savings;
    public bool IsSpending => T.Amount < 0 && !IsOwnMove && !IsSaving;
    public bool IsIncome => T.Amount > 0 && !IsOwnMove;
    public string Month => $"{T.Date:yyyy-MM}";
}

public enum FixedStatus { Paid, PaidChanged, Due, Late, Skipped, NotStarted }

public sealed record FixedPayment(
    string Key, string Name, string Category, Kind Kind, string Source,
    decimal Typical, int UsualDay, int MonthsSeen, DateOnly FirstPaid, DateOnly LastPaid,
    List<TxnInfo> Payments)
{
    /// <summary>Status of this payment in a given month (yyyy-MM).</summary>
    public (FixedStatus Status, decimal Paid, DateOnly? Date) StatusIn(string month, DateOnly today)
    {
        var inMonth = Payments.Where(p => p.Month == month).ToList();
        if (inMonth.Count > 0)
        {
            var paid = inMonth.Sum(p => -p.T.Amount);
            var changed = Math.Abs(paid - Typical) >= 1 && Math.Abs(paid - Typical) > Typical * 0.02m;
            return (changed ? FixedStatus.PaidChanged : FixedStatus.Paid, paid, inMonth.Max(p => p.T.Date));
        }
        if (string.CompareOrdinal(month, $"{FirstPaid:yyyy-MM}") < 0) return (FixedStatus.NotStarted, 0, null);
        if (month == $"{today:yyyy-MM}") return (today.Day <= UsualDay + 3 ? FixedStatus.Due : FixedStatus.Late, 0, null);
        return string.CompareOrdinal(month, $"{today:yyyy-MM}") > 0 ? (FixedStatus.Due, 0, null) : (FixedStatus.Skipped, 0, null);
    }
}

public sealed record MonthSummary(string Month, decimal Income, decimal Fixed, decimal Variable, decimal Saved, Dictionary<string, decimal> ByCategory)
{
    public decimal Spent => Fixed + Variable;
    public decimal Net => Income - Spent - Saved;
}

/// <summary>Turns raw transactions into categorised, series-grouped data and detects fixed (recurring) payments.</summary>
public sealed partial class Analysis
{
    public const string OwnTransfers = "Own transfers";
    public const string Savings = "Savings & investments";

    public List<TxnInfo> Items { get; }
    public List<FixedPayment> Fixed { get; }
    public List<MonthSummary> Months { get; }
    public List<string> AvailableMonths { get; }
    public string HolderName { get; }
    public IReadOnlyList<string> AllCategories { get; }

    private readonly UserRules _user;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public Analysis(List<Txn> txns, Dictionary<string, string[]> categoryRules, UserRules user)
    {
        _user = user = new UserRules { Recurring = user.Recurring, NotRecurring = user.NotRecurring, Categories = user.Categories, Names = new(user.Names) };
        var parsed = txns.Select(t => (t, raw: Raw.From(t))).ToList();

        // The account holder = name on the debtor side of our own debits.
        HolderName = parsed.Where(p => p.t.Amount < 0).Select(p => p.raw.DebtorName).Where(n => !string.IsNullOrWhiteSpace(n))
                           .GroupBy(n => Norm(n!)).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";

        // 1. classify
        var draft = parsed.Select(p => Classify(p.t, p.raw, categoryRules)).ToList();

        // 2. detect recurring series
        var monthsInData = draft.Select(d => d.Month).Distinct().Count();
        var recurring = new Dictionary<string, string>();   // key -> source
        foreach (var g in draft.Where(d => d.T.Amount < 0).GroupBy(d => d.SeriesKey))
        {
            var src = DetectRecurring(g.ToList(), monthsInData);
            if (src is not null) recurring[g.Key] = src;
        }
        foreach (var rule in Categories.FixedRules.Where(r => !string.IsNullOrWhiteSpace(r.Match)))
        {
            var words = rule.Match.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(w => w.ToUpperInvariant());
            foreach (var d in draft.Where(d => d.T.Amount < 0 && words.Any(w => Text(d.T).Contains(w))))
            {
                recurring[d.SeriesKey] = "your rule";
                if (!string.IsNullOrWhiteSpace(rule.Name)) user.Names.TryAdd(d.SeriesKey, rule.Name);
            }
        }
        foreach (var k in user.Recurring) recurring[k] = "marked by you";
        foreach (var k in user.NotRecurring) recurring.Remove(k);

        // 3. final items (user names / categories win)
        Items = draft.Select(d => d with
        {
            Name = user.Names.GetValueOrDefault(d.SeriesKey, d.Name),
            Category = user.Categories.GetValueOrDefault(d.SeriesKey, d.Category),
            IsRecurring = recurring.ContainsKey(d.SeriesKey) && d.T.Amount < 0
        }).OrderByDescending(i => i.T.Date).ThenBy(i => i.T.Amount).ToList();

        Fixed = Items.Where(i => i.IsRecurring).GroupBy(i => i.SeriesKey)
                     .Select(g => BuildFixed(g.Key, g.ToList(), recurring[g.Key]))
                     .OrderBy(f => f.UsualDay).ThenByDescending(f => f.Typical).ToList();
        DisambiguateNames();

        AvailableMonths = Items.Select(i => i.Month).Distinct().OrderDescending().ToList();
        if (!AvailableMonths.Contains($"{_today:yyyy-MM}")) AvailableMonths.Insert(0, $"{_today:yyyy-MM}");
        Months = AvailableMonths.OrderBy(m => m).Select(BuildMonth).ToList();

        AllCategories = categoryRules.Keys
            .Concat(["Loan", "Bills & direct debits", "Bank & fees", "Cash", "Transfers to others", Savings, OwnTransfers, "Income", "Other"])
            .Concat(user.Categories.Values).Distinct().OrderBy(c => c).ToList();
    }

    public MonthSummary MonthData(string month) => Months.FirstOrDefault(m => m.Month == month)
        ?? new MonthSummary(month, 0, 0, 0, 0, []);

    // ------------------------------------------------------------------ classification

    private TxnInfo Classify(Txn t, Raw r, Dictionary<string, string[]> rules)
    {
        var debit = t.Amount < 0;
        var other = debit ? r.CreditorName : r.DebtorName;
        var otherIban = debit ? r.CreditorIban : r.DebtorIban;
        var own = !string.IsNullOrEmpty(HolderName) && other is not null && Norm(other) == HolderName;
        var desc = t.Description;

        Kind kind = (r.Code, r.SubCode, debit) switch
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
        // fall back on text when the bank sent no code (older data)
        if (r.Code is null && debit)
        {
            if (desc.Contains("Direct debit", StringComparison.OrdinalIgnoreCase) || desc.Contains("Domiciliation", StringComparison.OrdinalIgnoreCase)) kind = Kind.DirectDebit;
            else if (desc.Contains("Payment with", StringComparison.OrdinalIgnoreCase) || desc.Contains("Debit Mastercard", StringComparison.OrdinalIgnoreCase)) kind = Kind.Card;
        }

        string name, key;
        switch (kind)
        {
            case Kind.Card or Kind.Refund:
                name = Merchant(desc.Length > 0 ? desc : t.Counterparty);
                key = "CARD:" + Norm(name);
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
                name = "Spare change investing";
                key = "SPARE";
                break;
            case Kind.CashWithdrawal or Kind.CashDeposit:
                name = kind == Kind.CashDeposit ? "Cash deposit" : "Cash withdrawal";
                key = kind.ToString().ToUpperInvariant();
                break;
            case Kind.LoanReceived:
                name = "Loan received";
                key = "LOANIN";
                break;
            default:
                name = Title(other ?? t.Counterparty);
                key = (debit ? "TO:" : "FROM:") + (otherIban ?? Norm(name));
                break;
        }

        var category = kind switch
        {
            Kind.Loan => "Loan",
            Kind.SavingsPlan or Kind.SpareChange => Savings,
            Kind.BankFee => "Bank & fees",
            Kind.OwnTransferOut or Kind.OwnTransferIn or Kind.LoanReceived or Kind.CashDeposit => OwnTransfers,
            Kind.CashWithdrawal => "Cash",
            Kind.Income => "Income",
            Kind.Refund => "Refunds",
            // card texts contain "Google Pay", "KBC", "Mastercard"… → only look at the shop name
            _ => KeywordCategory(name, rules) ?? (kind == Kind.Card ? null : KeywordCategory(desc, rules))
                 ?? kind switch { Kind.DirectDebit => "Bills & direct debits", Kind.Transfer => "Transfers to others", _ => "Other" }
        };

        return new TxnInfo(t, kind, key, name, category, false);
    }

    /// <summary>Category whose keyword matches; the longest keyword wins ("AUTOMOBILE" beats "CARREFOUR").</summary>
    private static string? KeywordCategory(string text, Dictionary<string, string[]> rules)
    {
        var u = " " + text.ToUpperInvariant() + " ";
        return rules.SelectMany(r => r.Value.Select(w => (cat: r.Key, w: w.ToUpperInvariant())))
                    .Where(x => x.w.Length > 0 && u.Contains(x.w))
                    .OrderByDescending(x => x.w.Trim().Length)
                    .Select(x => x.cat).FirstOrDefault();
    }

    // ------------------------------------------------------------------ recurring detection

    /// <summary>Returns why a series is recurring, or null.</summary>
    private static string? DetectRecurring(List<TxnInfo> s, int monthsInData)
    {
        var kind = s[0].Kind;
        if (kind is Kind.OwnTransferOut or Kind.SpareChange or Kind.CashWithdrawal or Kind.Other) return null;

        var byMonth = s.GroupBy(x => x.Month).ToList();
        var perMonth = (double)s.Count / byMonth.Count;
        if (perMonth > 1.5) return null;                       // several times a month = not a fixed payment

        // Contract-type payments: the bank itself says it's a mandate/loan/plan/fee → 2 months is enough
        if (kind is Kind.DirectDebit or Kind.Loan or Kind.SavingsPlan or Kind.BankFee)
            return byMonth.Count >= 2 ? kind switch
            {
                Kind.DirectDebit => "direct debit",
                Kind.Loan => "loan",
                Kind.SavingsPlan => "savings plan",
                _ => "bank fee"
            } : null;

        // Transfers and card payments: need regular months and a stable amount
        var needMonths = Math.Max(2, Math.Min(3, monthsInData - 1));
        if (byMonth.Count < needMonths) return null;
        var amounts = s.Select(x => -x.T.Amount).OrderBy(a => a).ToList();
        var median = amounts[amounts.Count / 2];
        if (median < 3) return null;
        var tolerance = kind == Kind.Card ? 0.05m : 0.15m;
        var stable = amounts.Count(a => Math.Abs(a - median) <= median * tolerance) >= Math.Ceiling(amounts.Count * 0.75);
        if (!stable) return null;
        var days = s.Select(x => x.T.Date.Day).OrderBy(d => d).ToList();
        var spread = days[^1] - days[0];
        if (spread > 8 && !(days[0] <= 5 && days[^1] >= 25)) return null;   // allow end/start-of-month wrap
        return kind == Kind.Card ? "same shop & amount monthly" : "standing transfer";
    }

    private FixedPayment BuildFixed(string key, List<TxnInfo> items, string source)
    {
        var ordered = items.OrderBy(i => i.T.Date).ToList();
        var byMonth = ordered.GroupBy(i => i.Month).Select(g => g.Sum(i => -i.T.Amount)).ToList();
        var recent = byMonth.TakeLast(3).OrderBy(a => a).ToList();
        var typical = recent[recent.Count / 2];
        var days = ordered.Select(i => i.T.Date.Day).OrderBy(d => d).ToList();
        var f = ordered[^1];
        return new FixedPayment(key, f.Name, f.Category, f.Kind, source, typical, days[days.Count / 2],
            byMonth.Count, ordered[0].T.Date, f.T.Date, ordered);
    }

    // Two direct debits from the same company (e.g. two insurance contracts) → add the amount to tell them apart
    private void DisambiguateNames()
    {
        foreach (var g in Fixed.GroupBy(f => f.Name).Where(g => g.Count() > 1).ToList())
            foreach (var f in g.ToList())
            {
                if (_user.Names.ContainsKey(f.Key)) continue;
                var i = Fixed.IndexOf(f);
                Fixed[i] = f with { Name = $"{f.Name} · {f.Typical.ToString("0.00", CultureInfo.GetCultureInfo("fr-BE"))} €" };
            }
    }

    private MonthSummary BuildMonth(string month)
    {
        var m = Items.Where(i => i.Month == month).ToList();
        return new MonthSummary(month,
            m.Where(i => i.IsIncome).Sum(i => i.T.Amount),
            m.Where(i => i.IsSpending && i.IsRecurring).Sum(i => -i.T.Amount),
            m.Where(i => i.IsSpending && !i.IsRecurring).Sum(i => -i.T.Amount),
            m.Where(i => i.IsSaving).Sum(i => -i.T.Amount),
            m.Where(i => i.IsSpending).GroupBy(i => i.Category).ToDictionary(g => g.Key, g => g.Sum(i => -i.T.Amount)));
    }

    // ------------------------------------------------------------------ text helpers

    public static string Text(Txn t) => $" {t.Counterparty} {t.Description} {t.CounterpartyIban} {t.BankCode} ".ToUpperInvariant();

    public static string Norm(string s)
    {
        var words = NonLetters().Replace(s.ToUpperInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).Take(4);
        var r = string.Join(' ', words);
        return r.Length > 0 ? r : s.Trim().ToUpperInvariant();
    }

    /// <summary>"STIB-MIVB ONLINE BE1000 Bruxelles Payment with…" → "Stib-Mivb Online"</summary>
    private static string Merchant(string text)
    {
        var m = CardMerchant().Match(text);
        var name = m.Success ? m.Groups[1].Value : Before(text, " Payment");
        name = PaymentPrefix().Replace(name, "").Trim();
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
    [GeneratedRegex(@"^(.*?)\s+[A-Z]{2}\d{3,6}\s")] private static partial Regex CardMerchant();
    [GeneratedRegex(@"^(SQ \*|SUMUP \*|SumUp \*|PAYPAL \*|\d{3,5}\s)", RegexOptions.IgnoreCase)] private static partial Regex PaymentPrefix();
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
