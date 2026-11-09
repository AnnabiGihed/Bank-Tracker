using System.Text.RegularExpressions;

namespace KbcTracker;

public sealed record FixedPayment(
    string Name,
    decimal TypicalAmount,
    int UsualDay,
    int MonthsSeen,
    DateOnly LastPaid,
    decimal LastAmount,
    bool PaidThisMonth,
    bool AmountChanged,
    List<Txn> Payments);

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
        var text = (" " + t.Counterparty + " " + t.Description + " ").ToUpperInvariant();
        foreach (var (cat, words) in _rules)
            if (words.Any(w => text.Contains(w.ToUpperInvariant()))) return cat;
        return "Other";
    }

    /// <summary>Same payee, at least 3 different months (or every month if less data), stable amount (±15%).</summary>
    private List<FixedPayment> DetectFixed()
    {
        var debits = _txns.Where(t => t.Amount < 0).ToList();
        if (debits.Count == 0) return [];

        var monthsInData = debits.Select(t => (t.Date.Year, t.Date.Month)).Distinct().Count();
        var minMonths = Math.Min(3, Math.Max(2, monthsInData));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = new List<FixedPayment>();
        foreach (var g in debits.GroupBy(t => Key(t)))
        {
            var byMonth = g.GroupBy(t => (t.Date.Year, t.Date.Month)).ToList();
            if (byMonth.Count < minMonths) continue;
            // a fixed payment is typically once per month
            if (byMonth.Average(m => m.Count()) > 1.5) continue;

            var amounts = g.Select(t => -t.Amount).OrderBy(a => a).ToList();
            var median = amounts[amounts.Count / 2];
            var stable = amounts.Count(a => Math.Abs(a - median) <= median * 0.15m) >= Math.Ceiling(amounts.Count * 0.75);
            if (!stable) continue;

            var ordered = g.OrderBy(t => t.Date).ToList();
            var last = ordered[^1];
            var days = ordered.Select(t => t.Date.Day).OrderBy(d => d).ToList();
            result.Add(new FixedPayment(
                Name: ordered.GroupBy(t => t.Counterparty).OrderByDescending(x => x.Count()).First().Key,
                TypicalAmount: median,
                UsualDay: days[days.Count / 2],
                MonthsSeen: byMonth.Count,
                LastPaid: last.Date,
                LastAmount: -last.Amount,
                PaidThisMonth: last.Date.Year == today.Year && last.Date.Month == today.Month,
                AmountChanged: Math.Abs(-last.Amount - median) > 0.01m * Math.Max(1, median) && Math.Abs(-last.Amount - median) >= 1,
                Payments: ordered));
        }
        return result.OrderByDescending(f => f.TypicalAmount).ToList();
    }

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
    private static string Key(Txn t)
    {
        var s = NonLetters().Replace(t.Counterparty.ToUpperInvariant(), " ");
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).Take(3);
        var key = string.Join(' ', words);
        return key.Length > 0 ? key : t.Counterparty;
    }

    [GeneratedRegex(@"[^A-ZÀ-ſ ]")]
    private static partial Regex NonLetters();
}
