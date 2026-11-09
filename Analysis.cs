using System.Text.RegularExpressions;

namespace KbcTracker;

public sealed record FixedPayment(
    string Name,
    decimal TypicalAmount,
    int UsualDay,
    int MonthsSeen,
    DateOnly? LastPaid,
    decimal? LastAmount,
    bool PaidThisMonth,
    bool AmountChanged,
    List<Txn> Payments,
    string Source);   // "manual", "payee", "amount"

public sealed record MonthSummary(string Month, decimal Income, decimal Fixed, decimal Variable, Dictionary<string, decimal> ByCategory);

/// <summary>Detects recurring (fixed) payments and builds monthly summaries.</summary>
public sealed partial class Analysis
{
    private readonly List<Txn> _txns;
    private readonly Dictionary<string, string[]> _rules;
    private readonly HashSet<string> _fixedIds;

    public List<FixedPayment> Fixed { get; }
    public List<MonthSummary> Months { get; }

    public Analysis(List<Txn> txns, Dictionary<string, string[]> rules)
    {
        _txns = txns;
        _rules = rules;
        Fixed = DetectFixed();
        _fixedIds = Fixed.SelectMany(f => f.Payments).Select(p => p.Id).ToHashSet();
        Months = BuildMonths();
    }

    public bool IsFixed(Txn t) => _fixedIds.Contains(t.Id);

    public string CategoryOf(Txn t)
    {
        if (t.Amount > 0) return "Income";
        if (IsFixed(t)) return "Fixed payments";
        var text = Text(t);
        foreach (var (cat, words) in _rules)
            if (words.Any(w => text.Contains(w.ToUpperInvariant()))) return cat;
        return "Other";
    }

    /// <summary>
    /// 1. Your own rules (appsettings "FixedPayments") — always win.
    /// 2. Same payee (IBAN, else normalised name) in ≥3 months, ~once a month, amount within ±15%.
    /// 3. Same amount (±2%) on about the same day each month — catches loans/transfers whose
    ///    description changes every month ("Aflossing oktober", "Aflossing november" …).
    /// </summary>
    private List<FixedPayment> DetectFixed()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var debits = _txns.Where(t => t.Amount < 0).ToList();
        var used = new HashSet<string>();
        var result = new List<FixedPayment>();

        // ---- 1. manual rules ----
        foreach (var rule in Categories.FixedRules.Where(r => !string.IsNullOrWhiteSpace(r.Match)))
        {
            var words = rule.Match.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var hits = debits.Where(t => !used.Contains(t.Id) && words.Any(w => Text(t).Contains(w.ToUpperInvariant())))
                             .Where(t => rule.Amount is null || Math.Abs(-t.Amount - rule.Amount.Value) <= rule.Amount.Value * 0.25m)
                             .ToList();
            hits.ForEach(t => used.Add(t.Id));
            result.Add(Build(rule.Name, hits, "manual", rule.Amount, rule.Day, today));
        }

        if (debits.Count == 0) return result;
        var monthsInData = debits.Select(t => (t.Date.Year, t.Date.Month)).Distinct().Count();
        var minMonths = Math.Min(3, Math.Max(2, monthsInData - 1));

        // ---- 2. same payee ----
        foreach (var g in debits.Where(t => !used.Contains(t.Id)).GroupBy(PayeeKey))
        {
            var list = g.ToList();
            if (!LooksMonthly(list, minMonths)) continue;
            var amounts = list.Select(t => -t.Amount).OrderBy(a => a).ToList();
            var median = amounts[amounts.Count / 2];
            if (amounts.Count(a => Math.Abs(a - median) <= median * 0.15m) < Math.Ceiling(amounts.Count * 0.75)) continue;
            list.ForEach(t => used.Add(t.Id));
            result.Add(Build(MostCommonName(list), list, "payee", null, null, today));
        }

        // how often each payee appears per month overall — shops you visit often are never "fixed"
        var payeeFreq = debits.GroupBy(PayeeKey).ToDictionary(g => g.Key,
            g => (double)g.Count() / g.Select(t => (t.Date.Year, t.Date.Month)).Distinct().Count());

        // ---- 3. same amount, same time of month ----
        var rest = debits.Where(t => !used.Contains(t.Id)).OrderBy(t => -t.Amount).ToList();
        while (rest.Count > 0)
        {
            var seed = rest[0];
            var amt = -seed.Amount;
            var cluster = rest.Where(t => Math.Abs(-t.Amount - amt) <= Math.Max(0.5m, amt * 0.02m)).ToList();
            rest = rest.Except(cluster).ToList();
            if (amt < 5 || !LooksMonthly(cluster, minMonths)) continue;
            if (cluster.Any(t => payeeFreq[PayeeKey(t)] > 1.5)) continue;
            var days = cluster.Select(t => t.Date.Day).OrderBy(d => d).ToList();
            if (days[^1] - days[0] > 6 && !(days[0] <= 3 && days[^1] >= 27)) continue;   // allow 30th→2nd wrap
            result.Add(Build(MostCommonName(cluster), cluster, "amount", null, null, today));
        }

        return result.OrderByDescending(f => f.TypicalAmount).ToList();
    }

    private static bool LooksMonthly(List<Txn> list, int minMonths)
    {
        var byMonth = list.GroupBy(t => (t.Date.Year, t.Date.Month)).ToList();
        return byMonth.Count >= minMonths && byMonth.Average(m => m.Count()) <= 1.5;
    }

    private static FixedPayment Build(string name, List<Txn> list, string source, decimal? expected, int? day, DateOnly today)
    {
        var ordered = list.OrderBy(t => t.Date).ToList();
        var amounts = ordered.Select(t => -t.Amount).OrderBy(a => a).ToList();
        var typical = expected ?? (amounts.Count > 0 ? amounts[amounts.Count / 2] : 0);
        var days = ordered.Select(t => t.Date.Day).OrderBy(d => d).ToList();
        var last = ordered.LastOrDefault();
        return new FixedPayment(
            Name: name,
            TypicalAmount: typical,
            UsualDay: day ?? (days.Count > 0 ? days[days.Count / 2] : 1),
            MonthsSeen: ordered.Select(t => (t.Date.Year, t.Date.Month)).Distinct().Count(),
            LastPaid: last?.Date,
            LastAmount: last is null ? null : -last.Amount,
            PaidThisMonth: last is not null && last.Date.Year == today.Year && last.Date.Month == today.Month,
            AmountChanged: last is not null && Math.Abs(-last.Amount - typical) >= 1 && Math.Abs(-last.Amount - typical) > typical * 0.01m,
            Payments: ordered,
            Source: source);
    }

    private static string MostCommonName(List<Txn> list) =>
        list.GroupBy(t => t.Counterparty).OrderByDescending(x => x.Count()).ThenByDescending(x => x.Max(t => t.Date)).First().Key;

    public static string Text(Txn t) =>
        $" {t.Counterparty} {t.Description} {t.CounterpartyIban} {t.BankCode} ".ToUpperInvariant();

    private List<MonthSummary> BuildMonths() =>
        _txns.GroupBy(t => $"{t.Date:yyyy-MM}")
             .OrderBy(g => g.Key)
             .Select(g => new MonthSummary(
                 g.Key,
                 g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                 g.Where(t => t.Amount < 0 && IsFixed(t)).Sum(t => -t.Amount),
                 g.Where(t => t.Amount < 0 && !IsFixed(t)).Sum(t => -t.Amount),
                 g.Where(t => t.Amount < 0 && !IsFixed(t))
                  .GroupBy(CategoryOf)
                  .ToDictionary(c => c.Key, c => c.Sum(t => -t.Amount))))
             .ToList();

    // Normalise payee names: drop digits/dates/references so "NETFLIX 12/09 REF 8812" == "NETFLIX 14/10 REF 9921"
    private static string PayeeKey(Txn t)
    {
        if (!string.IsNullOrEmpty(t.CounterpartyIban)) return "IBAN:" + t.CounterpartyIban;
        var s = NonLetters().Replace(t.Counterparty.ToUpperInvariant(), " ");
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).Take(3);
        var key = string.Join(' ', words);
        return key.Length > 0 ? key : t.Counterparty;
    }

    [GeneratedRegex(@"[^A-ZÀ-ſ ]")]
    private static partial Regex NonLetters();
}
