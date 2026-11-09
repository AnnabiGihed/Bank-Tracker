using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KbcTracker;

public static class Dashboard
{
    private static readonly CultureInfo Be = CultureInfo.GetCultureInfo("fr-BE");
    private static string E(string s) => WebUtility.HtmlEncode(s);
    private static string M(decimal d) => d.ToString("#,##0.00", Be) + " €";

    private const string Css = """
        :root{--bg:#f6f7f9;--card:#fff;--ink:#1d2433;--muted:#6b7385;--line:#e6e8ee;--fixed:#3b5bdb;--var:#f08c00;--inc:#2f9e44;--bad:#e03131}
        @media (prefers-color-scheme:dark){:root{--bg:#12151c;--card:#1b2029;--ink:#e8ebf2;--muted:#9aa3b5;--line:#2a303c}}
        *{box-sizing:border-box}body{margin:0;font:15px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--ink)}
        main{max-width:1100px;margin:0 auto;padding:20px 16px 60px}
        h1{font-size:22px;margin:0 0 4px}h2{font-size:16px;margin:0 0 12px}.muted{color:var(--muted);font-size:13px}
        .bar{display:flex;gap:8px;flex-wrap:wrap;align-items:center;justify-content:space-between;margin-bottom:18px}
        a.btn,button{background:var(--fixed);color:#fff;border:0;border-radius:8px;padding:8px 14px;text-decoration:none;font-weight:600;font-size:14px;cursor:pointer}
        a.btn.ghost{background:transparent;color:var(--ink);border:1px solid var(--line)}
        .grid{display:grid;gap:14px;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));margin-bottom:14px}
        .card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px}
        .kpi .v{font-size:24px;font-weight:700;margin-top:4px}.kpi .l{color:var(--muted);font-size:13px}
        table{width:100%;border-collapse:collapse;font-size:14px}th,td{text-align:left;padding:8px 6px;border-bottom:1px solid var(--line)}th{color:var(--muted);font-weight:600;font-size:12px;text-transform:uppercase;letter-spacing:.03em}
        td.num,th.num{text-align:right;font-variant-numeric:tabular-nums}
        .pill{display:inline-block;padding:2px 8px;border-radius:99px;font-size:12px;font-weight:600}
        .ok{background:#2f9e4422;color:var(--inc)}.due{background:#f08c0022;color:var(--var)}.chg{background:#e0313122;color:var(--bad)}
        .two{display:grid;gap:14px;grid-template-columns:1.4fr 1fr}@media(max-width:800px){.two{grid-template-columns:1fr}}
        .chart{position:relative;height:280px}
        """;

    public static string Render(Analysis a, List<Txn> txns, SessionInfo? session)
    {
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html lang=en><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>My expenses</title><style>{Css}</style>");
        sb.Append("<script src='https://cdn.jsdelivr.net/npm/chart.js@4'></script></head><body><main>");

        // Header
        sb.Append("<div class=bar><div><h1>My expenses</h1><div class=muted>");
        if (session is null) sb.Append("Not connected to the bank yet");
        else
        {
            sb.Append($"Last sync: {(session.LastSync?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "never")} · ");
            var daysLeft = (int)(session.ValidUntil - DateTimeOffset.UtcNow).TotalDays;
            sb.Append(daysLeft < 15 ? $"<b style='color:var(--bad)'>Bank consent expires in {daysLeft} days — reconnect</b>" : $"Bank consent valid {daysLeft} more days");
        }
        sb.Append("</div></div><div style='display:flex;gap:8px'>");
        sb.Append(session is null ? "<a class=btn href=/banks>Connect KBC Brussels</a>" : "<a class=btn href=/sync>Sync now</a><a class='btn ghost' href=/banks>Reconnect</a>");
        if (txns.Count > 0) sb.Append("<a class='btn ghost' href=/export.csv>Export CSV</a>");
        sb.Append("</div></div>");

        if (txns.Count == 0)
        {
            sb.Append("<div class=card><h2>No data yet</h2><p class=muted>Click <b>Connect KBC Brussels</b>, log in with itsme or your card reader, and your transactions will be downloaded automatically.</p></div></main></body></html>");
            return sb.ToString();
        }

        // KPIs: last 3 full months average + this month
        var thisMonth = $"{DateTime.Today:yyyy-MM}";
        var cur = a.Months.FirstOrDefault(m => m.Month == thisMonth);
        var full = a.Months.Where(m => m.Month != thisMonth).TakeLast(3).ToList();
        decimal Avg(Func<MonthSummary, decimal> f) => full.Count == 0 ? 0 : full.Average(f);
        var fixedTotal = a.Fixed.Sum(f => f.TypicalAmount);
        var dueLeft = a.Fixed.Where(f => !f.PaidThisMonth).Sum(f => f.TypicalAmount);

        sb.Append("<div class=grid>");
        Kpi(sb, "Fixed payments / month", M(fixedTotal), $"{a.Fixed.Count} recurring payments");
        Kpi(sb, "Still due this month", M(dueLeft), $"{a.Fixed.Count(f => !f.PaidThisMonth)} not paid yet");
        Kpi(sb, "Spent this month", M((cur?.Fixed ?? 0) + (cur?.Variable ?? 0)), $"variable: {M(cur?.Variable ?? 0)}");
        Kpi(sb, "Avg monthly variable", M(Avg(m => m.Variable)), $"income avg: {M(Avg(m => m.Income))}");
        sb.Append("</div>");

        // Fixed payments table
        sb.Append("<div class='card' style='margin-bottom:14px'><h2>Fixed payments</h2><table><tr><th>Payee</th><th class=num>Usual</th><th class=num>Day</th><th class=num>Last paid</th><th>This month</th></tr>");
        foreach (var f in a.Fixed)
        {
            var status = f.PaidThisMonth
                ? (f.AmountChanged ? $"<span class='pill chg'>paid {M(f.LastAmount)} — changed</span>" : "<span class='pill ok'>paid</span>")
                : $"<span class='pill due'>due ~{f.UsualDay}</span>";
            sb.Append($"<tr><td>{E(f.Name)}<div class=muted>{f.MonthsSeen} months seen</div></td><td class=num>{M(f.TypicalAmount)}</td><td class=num>{f.UsualDay}</td><td class=num>{f.LastPaid:dd/MM}</td><td>{status}</td></tr>");
        }
        if (a.Fixed.Count == 0) sb.Append("<tr><td colspan=5 class=muted>Not enough history yet to detect recurring payments (needs 2–3 months).</td></tr>");
        sb.Append($"<tr><th>Total</th><th class=num>{M(fixedTotal)}</th><th colspan=3></th></tr></table></div>");

        // Charts
        var months = a.Months.TakeLast(12).ToList();
        var cats = months.SelectMany(m => m.ByCategory.Keys).Distinct().OrderByDescending(c => months.Sum(m => m.ByCategory.GetValueOrDefault(c))).ToList();
        var catLast = (cur ?? months.Last()).ByCategory.OrderByDescending(kv => kv.Value).ToList();

        sb.Append("<div class=two><div class=card><h2>Month by month</h2><div class=chart><canvas id=monthly></canvas></div></div>");
        sb.Append($"<div class=card><h2>Variable spending — {(cur ?? months.Last()).Month}</h2><div class=chart><canvas id=cats></canvas></div></div></div>");

        // Recent transactions
        sb.Append("<div class=card style='margin-top:14px'><h2>Recent transactions</h2><table><tr><th>Date</th><th>Payee</th><th>Category</th><th class=num>Amount</th></tr>");
        foreach (var t in txns.OrderByDescending(t => t.Date).Take(40))
        {
            var color = t.Amount > 0 ? "var(--inc)" : "inherit";
            sb.Append($"<tr><td>{t.Date:dd/MM}</td><td>{E(t.Counterparty)}<div class=muted>{E(t.Description.Length > 70 ? t.Description[..70] + "…" : t.Description)}</div></td><td>{E(a.CategoryOf(t))}</td><td class=num style='color:{color}'>{M(t.Amount)}</td></tr>");
        }
        sb.Append("</table></div>");

        var data = JsonSerializer.Serialize(new
        {
            labels = months.Select(m => m.Month),
            fixedv = months.Select(m => m.Fixed),
            variable = months.Select(m => m.Variable),
            income = months.Select(m => m.Income),
            catLabels = catLast.Select(kv => kv.Key),
            catValues = catLast.Select(kv => kv.Value)
        });
        sb.Append("""
            <script>
            const d=__DATA__;
            const css=n=>getComputedStyle(document.documentElement).getPropertyValue(n).trim();
            Chart.defaults.color=css('--muted');Chart.defaults.borderColor=css('--line');
            new Chart(monthly,{type:'bar',data:{labels:d.labels,datasets:[
              {label:'Fixed',data:d.fixedv,backgroundColor:css('--fixed'),stack:'s'},
              {label:'Variable',data:d.variable,backgroundColor:css('--var'),stack:'s'},
              {label:'Income',data:d.income,type:'line',borderColor:css('--inc'),backgroundColor:css('--inc'),tension:.3}]},
              options:{maintainAspectRatio:false,plugins:{tooltip:{callbacks:{label:c=>`${c.dataset.label}: ${c.raw.toLocaleString('fr-BE',{minimumFractionDigits:2})} €`}}}}});
            new Chart(cats,{type:'bar',data:{labels:d.catLabels,datasets:[{data:d.catValues,backgroundColor:css('--var')}]},
              options:{indexAxis:'y',maintainAspectRatio:false,plugins:{legend:{display:false}}}});
            </script></main></body></html>
            """.Replace("__DATA__", data));
        return sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string label, string value, string sub) =>
        sb.Append($"<div class='card kpi'><div class=l>{E(label)}</div><div class=v>{value}</div><div class=muted>{E(sub)}</div></div>");

    public static string Banks(List<(string Name, string Country)> banks)
    {
        var sb = new StringBuilder($"<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Choose bank</title><style>{Css}</style></head><body><main><h1>Choose your bank</h1><p class=muted>KBC entries are listed first.</p><div class=card><table>");
        foreach (var b in banks.OrderByDescending(b => b.Name.Contains("KBC", StringComparison.OrdinalIgnoreCase)).ThenBy(b => b.Name))
            sb.Append($"<tr><td>{E(b.Name)}</td><td style='text-align:right'><a class=btn href='/connect?name={Uri.EscapeDataString(b.Name)}&country={b.Country}'>Connect</a></td></tr>");
        sb.Append("</table></div></main></body></html>");
        return sb.ToString();
    }
}
