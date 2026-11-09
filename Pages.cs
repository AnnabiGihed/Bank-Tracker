using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KbcTracker;

/// <summary>Server-rendered pages. One tab = one page, all driven by the selected month (?m=yyyy-MM).</summary>
public static class Pages
{
    private static readonly CultureInfo Be = CultureInfo.GetCultureInfo("fr-BE");
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string M(decimal d) => d.ToString("#,##0.00", Be) + " €";
    private static string M0(decimal d) => d.ToString("#,##0", Be) + " €";
    private static string MonthName(string m) =>
        DateTime.ParseExact(m + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
    private static string Js(object o) => JsonSerializer.Serialize(o);

    // ================================================================== layout

    private const string Css = """
        :root{--bg:#f4f5f8;--card:#fff;--ink:#1c2230;--muted:#6a7386;--line:#e4e7ee;--soft:#f0f2f7;
              --brand:#3454d1;--fixed:#3454d1;--var:#f2994a;--inc:#27ae60;--save:#9b51e0;--bad:#e5484d;--warn:#f2994a}
        @media (prefers-color-scheme:dark){:root{--bg:#0f1218;--card:#171b23;--ink:#e6e9f0;--muted:#8c95a8;--line:#262c38;--soft:#1e2430}}
        *{box-sizing:border-box}html,body{margin:0}body{font:14px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;background:var(--bg);color:var(--ink)}
        a{color:inherit}
        header{position:sticky;top:0;z-index:5;background:var(--card);border-bottom:1px solid var(--line)}
        .top{max-width:1180px;margin:0 auto;padding:12px 18px 0;display:flex;align-items:center;gap:14px;flex-wrap:wrap}
        .brand{font-weight:700;font-size:17px;display:flex;align-items:center;gap:8px}
        .brand i{width:26px;height:26px;border-radius:8px;background:var(--brand);display:inline-block}
        .sync{color:var(--muted);font-size:12px;margin-right:auto}
        .month{display:flex;align-items:center;gap:4px}
        .month a,.month select{border:1px solid var(--line);background:var(--card);color:var(--ink);border-radius:8px;height:32px;padding:0 10px;text-decoration:none;display:flex;align-items:center;font:inherit}
        .month select{font-weight:600;cursor:pointer}
        nav{max-width:1180px;margin:0 auto;padding:0 18px;display:flex;gap:4px;overflow-x:auto}
        nav a{padding:10px 14px;text-decoration:none;color:var(--muted);font-weight:600;border-bottom:2px solid transparent;white-space:nowrap}
        nav a.on{color:var(--brand);border-color:var(--brand)}
        main{max-width:1180px;margin:0 auto;padding:18px}
        .grid{display:grid;gap:12px}.k5{grid-template-columns:repeat(5,1fr)}.c2{grid-template-columns:3fr 2fr}.c2e{grid-template-columns:1fr 1fr}
        @media(max-width:900px){.k5{grid-template-columns:repeat(2,1fr)}.c2,.c2e{grid-template-columns:1fr}}
        .card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;min-width:0}
        .card h2{font-size:13px;margin:0 0 10px;color:var(--muted);text-transform:uppercase;letter-spacing:.04em;display:flex;justify-content:space-between;align-items:center}
        .kpi .v{font-size:22px;font-weight:700;margin-top:2px;font-variant-numeric:tabular-nums}.kpi .s{color:var(--muted);font-size:12px}
        .kpi{border-top:3px solid var(--line)}.kpi.inc{border-top-color:var(--inc)}.kpi.fix{border-top-color:var(--fixed)}.kpi.var{border-top-color:var(--var)}.kpi.sav{border-top-color:var(--save)}
        table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:8px 6px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{color:var(--muted);font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.04em;position:sticky;top:0;background:var(--card)}
        tr:last-child td{border-bottom:0}
        .num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
        .muted{color:var(--muted);font-size:12px}.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:420px}
        .pill{display:inline-block;padding:2px 9px;border-radius:99px;font-size:12px;font-weight:600;white-space:nowrap}
        .ok{background:#27ae6020;color:var(--inc)}.due{background:#f2994a22;color:var(--warn)}.bad{background:#e5484d1f;color:var(--bad)}.nil{background:var(--soft);color:var(--muted)}
        .tag{display:inline-block;padding:1px 7px;border-radius:6px;font-size:11px;background:var(--soft);color:var(--muted);margin-left:6px}
        .btn{display:inline-flex;align-items:center;gap:6px;background:var(--brand);color:#fff;border:0;border-radius:8px;padding:7px 12px;text-decoration:none;font-weight:600;font:inherit;font-weight:600;cursor:pointer}
        .btn.ghost{background:transparent;color:var(--ink);border:1px solid var(--line)}
        .icon{border:0;background:transparent;cursor:pointer;color:var(--muted);font-size:16px;padding:2px 6px;border-radius:6px}.icon:hover{background:var(--soft);color:var(--ink)}
        .icon.on{color:#f2b01e}
        .scroll{max-height:calc(100vh - 260px);overflow:auto}
        .chart{position:relative;height:220px}
        .filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px}
        .filters input,.filters select,td select{border:1px solid var(--line);background:var(--card);color:var(--ink);border-radius:8px;padding:6px 8px;font:inherit}
        td select{padding:3px 6px;font-size:12px;max-width:170px;background:transparent}
        .bar{height:8px;border-radius:4px;background:var(--soft);overflow:hidden}.bar i{display:block;height:100%;background:var(--var)}
        .stat{display:flex;gap:18px;flex-wrap:wrap}.stat div b{display:block;font-size:18px}
        .empty{padding:30px;text-align:center;color:var(--muted)}
        """;

    private static readonly (string Id, string Label)[] Tabs =
        [("overview", "Overview"), ("fixed", "Fixed payments"), ("transactions", "Transactions"), ("categories", "Categories"), ("settings", "Settings")];

    private static string Layout(string tab, string month, Analysis a, SessionInfo? s, string body, string? script = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<!doctype html><html lang=en><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>My expenses</title><style>{Css}</style>");
        sb.Append("<script src='https://cdn.jsdelivr.net/npm/chart.js@4'></script></head><body><header><div class=top>");
        sb.Append("<div class=brand><i></i>My expenses</div><div class=sync>");
        if (s is null) sb.Append("Not connected");
        else
        {
            var left = (int)(s.ValidUntil - DateTimeOffset.UtcNow).TotalDays;
            sb.Append($"Synced {s.LastSync?.ToLocalTime():dd/MM HH:mm}");
            if (a.Items.Count > 0) sb.Append($" · data from {a.Items.Min(i => i.T.Date):dd/MM/yyyy}");
            if (left < 15) sb.Append($" · <b style='color:var(--bad)'>bank consent ends in {left} days</b>");
        }
        sb.Append("</div>");
        if (tab != "settings")
        {
            var i = a.AvailableMonths.IndexOf(month);
            var older = i >= 0 && i < a.AvailableMonths.Count - 1 ? a.AvailableMonths[i + 1] : null;
            var newer = i > 0 ? a.AvailableMonths[i - 1] : null;
            sb.Append("<div class=month>");
            sb.Append(older is null ? "<a style='opacity:.3'>‹</a>" : $"<a href='?m={older}' title='Previous month'>‹</a>");
            sb.Append("<select onchange=\"location.search='?m='+this.value\">");
            foreach (var m in a.AvailableMonths) sb.Append($"<option value={m}{(m == month ? " selected" : "")}>{MonthName(m)}</option>");
            sb.Append("</select>");
            sb.Append(newer is null ? "<a style='opacity:.3'>›</a>" : $"<a href='?m={newer}' title='Next month'>›</a>");
            sb.Append("</div>");
        }
        sb.Append(s is null ? "<a class=btn href=/banks>Connect bank</a>" : $"<a class=btn href='/sync?back=/{tab}?m={month}'>↻ Sync</a>");
        sb.Append("</div><nav>");
        foreach (var (id, label) in Tabs)
            sb.Append($"<a class='{(id == tab ? "on" : "")}' href='/{id}?m={month}'>{label}</a>");
        sb.Append("</nav></header><main>").Append(body).Append("</main>");
        sb.Append("""
            <script>
            async function rule(action,key,value){
              await fetch('/api/rules',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,key,value:value??null})});
              location.reload();
            }
            const css=n=>getComputedStyle(document.documentElement).getPropertyValue(n).trim();
            if(window.Chart){Chart.defaults.color=css('--muted');Chart.defaults.borderColor=css('--line');Chart.defaults.font.family='system-ui'}
            const eur=v=>v.toLocaleString('fr-BE',{minimumFractionDigits:2,maximumFractionDigits:2})+' €';
            </script>
            """);
        if (script is not null) sb.Append("<script>").Append(script).Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Empty(string text) =>
        $"<div class=card><div class=empty>{text}</div></div>";

    // ================================================================== Overview

    public static string Overview(Analysis a, string month, SessionInfo? s)
    {
        if (a.Items.Count == 0)
            return Layout("overview", month, a, s, Empty(s is null
                ? "Connect your bank to get started: click <b>Connect bank</b> and log in with itsme."
                : "No transactions yet — click <b>↻ Sync</b>."));

        var cur = a.MonthData(month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fixedStatus = a.Fixed.Select(f => (f, st: f.StatusIn(month, today))).Where(x => x.st.Status != FixedStatus.NotStarted).ToList();
        var expected = fixedStatus.Sum(x => x.f.Kind is Kind.SavingsPlan ? 0 : x.f.Typical);
        var open = fixedStatus.Where(x => x.st.Status is FixedStatus.Due or FixedStatus.Late or FixedStatus.Skipped).ToList();

        var sb = new StringBuilder("<div class='grid k5' style='margin-bottom:12px'>");
        Kpi(sb, "inc", "Income", M(cur.Income), "salary, refunds…");
        Kpi(sb, "", "Spent", M(cur.Spent), $"fixed {M0(cur.Fixed)} · variable {M0(cur.Variable)}");
        Kpi(sb, "fix", "Fixed payments", M(cur.Fixed), open.Count == 0 ? "all paid" : $"{open.Count} still to come · {M0(open.Sum(x => x.f.Typical))}");
        Kpi(sb, "sav", "Saved & invested", M(cur.Saved), "pension plan, spare change");
        Kpi(sb, "var", "Left over", M(cur.Net), "income − spent − saved");
        sb.Append("</div>");

        sb.Append("<div class='grid c2' style='margin-bottom:12px'>");
        sb.Append("<div class=card><h2>Last months</h2><div class=chart><canvas id=months></canvas></div></div>");
        sb.Append($"<div class=card><h2>Where it went — {MonthName(month)}</h2><div class=chart><canvas id=cats></canvas></div></div></div>");

        sb.Append("<div class='grid c2e'>");
        sb.Append($"<div class=card><h2>Fixed payments this month <a class=muted href='/fixed?m={month}'>see all →</a></h2><table>");
        foreach (var (f, st) in fixedStatus.OrderBy(x => x.st.Status is FixedStatus.Paid or FixedStatus.PaidChanged).ThenBy(x => x.f.UsualDay).Take(6))
            sb.Append($"<tr><td>{E(f.Name)}</td><td class=num>{M(st.Paid > 0 ? st.Paid : f.Typical)}</td><td style='width:1%'>{StatusPill(st, f)}</td></tr>");
        if (fixedStatus.Count == 0) sb.Append("<tr><td class=muted>No fixed payments detected yet.</td></tr>");
        sb.Append($"</table><div class=muted style='margin-top:8px'>{fixedStatus.Count(x => x.st.Status is FixedStatus.Paid or FixedStatus.PaidChanged)} of {fixedStatus.Count} paid · expected total {M(expected)}</div></div>");

        sb.Append($"<div class=card><h2>Biggest expenses <a class=muted href='/transactions?m={month}'>all transactions →</a></h2><table>");
        foreach (var i in a.Items.Where(i => i.Month == month && i.IsSpending).OrderBy(i => i.T.Amount).Take(8))
            sb.Append($"<tr><td class=muted style='width:1%'>{i.T.Date:dd/MM}</td><td>{E(i.Name)}<span class=tag>{E(i.Category)}</span></td><td class=num>{M(-i.T.Amount)}</td></tr>");
        sb.Append("</table></div></div>");

        var months = a.Months.TakeLast(12).ToList();
        var cats = cur.ByCategory.OrderByDescending(kv => kv.Value).ToList();
        var data = Js(new { l = months.Select(m => MonthName(m.Month)[..3] + " " + m.Month[2..4]), f = months.Select(m => m.Fixed), v = months.Select(m => m.Variable), s = months.Select(m => m.Saved), i = months.Select(m => m.Income), cl = cats.Select(c => c.Key), cv = cats.Select(c => c.Value) });
        var script = """
            const d=__D__;
            new Chart(document.getElementById('months'),{type:'bar',data:{labels:d.l,datasets:[
              {label:'Fixed',data:d.f,backgroundColor:css('--fixed'),stack:'s',borderRadius:4},
              {label:'Variable',data:d.v,backgroundColor:css('--var'),stack:'s',borderRadius:4},
              {label:'Saved',data:d.s,backgroundColor:css('--save'),stack:'s',borderRadius:4},
              {label:'Income',data:d.i,type:'line',borderColor:css('--inc'),backgroundColor:css('--inc'),tension:.3,pointRadius:3}]},
              options:{maintainAspectRatio:false,interaction:{mode:'index',intersect:false},plugins:{legend:{position:'bottom'},tooltip:{callbacks:{label:c=>`${c.dataset.label}: ${eur(c.raw)}`}}},scales:{y:{ticks:{callback:v=>v.toLocaleString('fr-BE')+' €'}}}}});
            const pal=['#3454d1','#f2994a','#27ae60','#9b51e0','#eb5757','#2d9cdb','#f2c94c','#6fcf97','#bb6bd9','#56ccf2','#828282','#219653'];
            new Chart(document.getElementById('cats'),{type:'doughnut',data:{labels:d.cl,datasets:[{data:d.cv,backgroundColor:pal,borderWidth:0}]},
              options:{maintainAspectRatio:false,cutout:'62%',plugins:{legend:{position:'right',labels:{boxWidth:10}},tooltip:{callbacks:{label:c=>`${c.label}: ${eur(c.raw)}`}}},
              onClick:(e,el)=>{if(el.length)location.href='/transactions?m=__M__&cat='+encodeURIComponent(d.cl[el[0].index])}}});
            """.Replace("__D__", data).Replace("__M__", month);
        return Layout("overview", month, a, s, sb.ToString(), script);
    }

    private static void Kpi(StringBuilder sb, string cls, string label, string value, string sub) =>
        sb.Append($"<div class='card kpi {cls}'><div class=muted>{E(label)}</div><div class=v>{value}</div><div class=s>{E(sub)}</div></div>");

    private static string StatusPill((FixedStatus Status, decimal Paid, DateOnly? Date) st, FixedPayment f) => st.Status switch
    {
        FixedStatus.Paid => $"<span class='pill ok'>paid {st.Date:dd/MM}</span>",
        FixedStatus.PaidChanged => $"<span class='pill bad' title='Usually {M(f.Typical)}'>paid {M(st.Paid)} ≠ {M0(f.Typical)}</span>",
        FixedStatus.Due => $"<span class='pill due'>due ~{f.UsualDay}</span>",
        FixedStatus.Late => $"<span class='pill bad'>expected ~{f.UsualDay}, not seen</span>",
        FixedStatus.Skipped => "<span class='pill nil'>not paid this month</span>",
        _ => "<span class='pill nil'>—</span>"
    };

    // ================================================================== Fixed payments

    public static string Fixed(Analysis a, string month, SessionInfo? s, UserRules user)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = a.Fixed.Select(f => (f, st: f.StatusIn(month, today))).Where(x => x.st.Status != FixedStatus.NotStarted).ToList();
        var expected = rows.Sum(x => x.f.Typical);
        var paid = rows.Sum(x => x.st.Paid);
        var toCome = rows.Where(x => x.st.Status is FixedStatus.Due or FixedStatus.Late).Sum(x => x.f.Typical);

        var sb = new StringBuilder("<div class='card' style='margin-bottom:12px'><div class=stat>");
        sb.Append($"<div><span class=muted>Expected in {MonthName(month)}</span><b>{M(expected)}</b></div>");
        sb.Append($"<div><span class=muted>Paid</span><b style='color:var(--inc)'>{M(paid)}</b></div>");
        sb.Append($"<div><span class=muted>Still to come</span><b style='color:var(--warn)'>{M(toCome)}</b></div>");
        sb.Append($"<div><span class=muted>Payments</span><b>{rows.Count}</b></div></div></div>");

        sb.Append("<div class=card><div class=scroll><table><tr><th class=num>Day</th><th>Payment</th><th>Category</th><th class=num>Usual</th><th>This month</th><th></th></tr>");
        foreach (var (f, st) in rows.OrderBy(x => x.f.UsualDay))
            sb.Append($"""
                <tr><td class=num>{f.UsualDay}</td>
                <td><b>{E(f.Name)}</b><div class=muted>{E(f.Source)} · seen {f.MonthsSeen} month(s) · since {f.FirstPaid:dd/MM/yy}</div></td>
                <td>{E(f.Category)}</td><td class=num>{M(f.Typical)}</td><td>{StatusPill(st, f)}</td>
                <td class=num><button class=icon title='Rename' onclick="const n=prompt('Name',{E(Js(f.Name))});if(n)rule('name',{E(Js(f.Key))},n)">✎</button><button class=icon title='Not a fixed payment' onclick="rule('notrecurring',{E(Js(f.Key))})">✕</button></td></tr>
                """);
        if (rows.Count == 0) sb.Append("<tr><td colspan=6 class=empty>No fixed payments for this month.</td></tr>");
        sb.Append("</table></div>");
        sb.Append("<div class=muted style='margin-top:10px'>Missing one? Open <b>Transactions</b> and click ☆ next to it. Wrong one? Click ✕.</div></div>");

        var ignored = user.NotRecurring.Select(k => (k, name: a.Items.FirstOrDefault(i => i.SeriesKey == k)?.Name ?? k)).ToList();
        if (ignored.Count > 0)
        {
            sb.Append("<div class=card style='margin-top:12px'><h2>Marked as not fixed</h2><table>");
            foreach (var (k, name) in ignored)
                sb.Append($"<tr><td>{E(name)}</td><td class=num><button class='btn ghost' onclick=\"rule('restore',{E(Js(k))})\">Restore</button></td></tr>");
            sb.Append("</table></div>");
        }
        return Layout("fixed", month, a, s, sb.ToString());
    }

    // ================================================================== Transactions

    public static string Transactions(Analysis a, string month, SessionInfo? s, string? cat)
    {
        var items = a.Items.Where(i => i.Month == month).ToList();
        var sb = new StringBuilder("<div class=filters>");
        sb.Append("<input id=q placeholder='Search name, text or amount…' style='flex:1;min-width:200px'>");
        sb.Append("<select id=type><option value=spend>Spending</option><option value=income>Income</option><option value=save>Savings</option><option value=own>Own transfers</option><option value=all>Everything</option></select>");
        sb.Append("<select id=cat><option value=''>All categories</option>");
        foreach (var c in items.Select(i => i.Category).Distinct().OrderBy(c => c))
            sb.Append($"<option{(c == cat ? " selected" : "")}>{E(c)}</option>");
        sb.Append("</select></div>");

        sb.Append("<div class=card><div class=scroll><table id=tx><tr><th>Date</th><th>Name</th><th>Category</th><th class=num>Amount</th><th title='Fixed payment'>Fixed</th></tr>");
        foreach (var i in items)
        {
            var type = i.IsOwnMove ? "own" : i.IsSaving ? "save" : i.IsIncome ? "income" : i.IsSpending ? "spend" : "other";
            var search = $"{i.Name} {i.T.Description} {Math.Abs(i.T.Amount).ToString("0.00", Be)}".ToLowerInvariant();
            sb.Append($"<tr data-type={type} data-cat='{E(i.Category)}' data-s='{E(search)}' data-a='{(-i.T.Amount).ToString(CultureInfo.InvariantCulture)}'>");
            sb.Append($"<td class=muted style='width:1%'>{i.T.Date:dd/MM}</td>");
            sb.Append($"<td><b>{E(i.Name)}</b><div class='muted ell' title='{E(i.T.Description)}'>{E(i.T.Description)}</div></td>");
            sb.Append($"<td><select onchange=\"rule('category',{E(Js(i.SeriesKey))},this.value)\" title='Applies to all payments from {E(i.Name)}'>");
            foreach (var c in a.AllCategories) sb.Append($"<option{(c == i.Category ? " selected" : "")}>{E(c)}</option>");
            sb.Append("</select></td>");
            sb.Append($"<td class=num style='color:{(i.T.Amount > 0 ? "var(--inc)" : "inherit")}'>{M(i.T.Amount)}</td>");
            sb.Append(i.T.Amount < 0
                ? $"<td style='width:1%'><button class='icon {(i.IsRecurring ? "on" : "")}' title='{(i.IsRecurring ? "Fixed payment — click to unmark" : "Mark as fixed payment")}' onclick=\"rule('{(i.IsRecurring ? "notrecurring" : "recurring")}',{E(Js(i.SeriesKey))})\">{(i.IsRecurring ? "★" : "☆")}</button></td>"
                : "<td></td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        if (items.Count == 0) sb.Append("<div class=empty>No transactions in this month.</div>");
        sb.Append("</div><div class=muted style='margin-top:10px;display:flex;justify-content:space-between'><span id=count></span><b id=total></b></div></div>");

        const string script = """
            const q=document.getElementById('q'),type=document.getElementById('type'),cat=document.getElementById('cat');
            if(cat.value)type.value='all';
            function apply(){
              let n=0,t=0;const s=q.value.trim().toLowerCase();
              document.querySelectorAll('#tx tr[data-type]').forEach(r=>{
                const ok=(type.value==='all'||r.dataset.type===type.value)&&(!cat.value||r.dataset.cat===cat.value)&&(!s||r.dataset.s.includes(s));
                r.style.display=ok?'':'none';if(ok){n++;t+=parseFloat(r.dataset.a)}});
              document.getElementById('count').textContent=n+' transactions';
              document.getElementById('total').textContent=(t>=0?'Total out: ':'Total in: ')+eur(Math.abs(t));
            }
            [q,type,cat].forEach(e=>e.addEventListener('input',apply));apply();
            """;
        return Layout("transactions", month, a, s, sb.ToString(), script);
    }

    // ================================================================== Categories

    public static string CategoriesPage(Analysis a, string month, SessionInfo? s)
    {
        var cur = a.MonthData(month);
        var prev = a.Months.Where(m => string.CompareOrdinal(m.Month, month) < 0).TakeLast(3).ToList();
        var total = cur.ByCategory.Values.Sum();
        var max = cur.ByCategory.Values.DefaultIfEmpty(1).Max();

        var sb = new StringBuilder($"<div class=card><h2>Spending by category — {MonthName(month)}<span>{M(total)}</span></h2><table><tr><th>Category</th><th style='width:40%'></th><th class=num>Amount</th><th class=num>Share</th><th class=num>vs. avg of previous months</th></tr>");
        foreach (var (c, v) in cur.ByCategory.OrderByDescending(kv => kv.Value))
        {
            var avg = prev.Count == 0 ? (decimal?)null : prev.Average(m => m.ByCategory.GetValueOrDefault(c));
            var diff = avg is null || avg == 0 ? "<span class=muted>—</span>"
                : v > avg * 1.15m ? $"<span class='pill bad'>+{M0(v - avg.Value)}</span>"
                : v < avg * 0.85m ? $"<span class='pill ok'>−{M0(avg.Value - v)}</span>"
                : "<span class='pill nil'>similar</span>";
            sb.Append($"<tr style='cursor:pointer' onclick=\"location.href='/transactions?m={month}&cat={Uri.EscapeDataString(c)}'\"><td><b>{E(c)}</b></td><td><div class=bar><i style='width:{(max == 0 ? 0 : v / max * 100):0}%'></i></div></td><td class=num>{M(v)}</td><td class=num>{(total == 0 ? 0 : v / total * 100):0}%</td><td class=num>{diff}</td></tr>");
        }
        if (cur.ByCategory.Count == 0) sb.Append("<tr><td colspan=5 class=empty>No spending in this month.</td></tr>");
        sb.Append("</table><div class=muted style='margin-top:10px'>Own transfers and savings are not counted as spending. Click a category to see its transactions; change a category from the Transactions tab.</div></div>");
        return Layout("categories", month, a, s, sb.ToString());
    }

    // ================================================================== Settings

    public static string Settings(Analysis a, string month, SessionInfo? s, UserRules user)
    {
        var sb = new StringBuilder("<div class='grid c2e'>");
        sb.Append("<div class=card><h2>Bank connection</h2>");
        if (s is null) sb.Append("<p>Not connected.</p><a class=btn href=/banks>Connect KBC Brussels</a>");
        else
        {
            sb.Append($"""
                <table>
                <tr><td class=muted>Accounts</td><td>{s.AccountUids.Count}</td></tr>
                <tr><td class=muted>Connected on</td><td>{s.ConnectedAt.ToLocalTime():dd/MM/yyyy}</td></tr>
                <tr><td class=muted>Consent valid until</td><td>{s.ValidUntil.ToLocalTime():dd/MM/yyyy}</td></tr>
                <tr><td class=muted>Last sync</td><td>{s.LastSync?.ToLocalTime():dd/MM/yyyy HH:mm}</td></tr>
                <tr><td class=muted>History stored</td><td>{(a.Items.Count == 0 ? "—" : $"{a.Items.Min(i => i.T.Date):dd/MM/yyyy} → {a.Items.Max(i => i.T.Date):dd/MM/yyyy} ({a.Items.Count} transactions)")}</td></tr>
                </table>
                <div style='display:flex;gap:8px;flex-wrap:wrap;margin-top:12px'>
                  <a class=btn href='/sync?back=/settings'>↻ Sync now</a>
                  <a class='btn ghost' href='/sync?full=true&back=/settings'>Full re-sync</a>
                  <a class='btn ghost' href=/banks>Reconnect / add account</a>
                </div>
                <p class=muted>KBC only shares about 90 days of history. Everything synced is kept here, so history grows over time — sync at least once every 2–3 months.</p>
                """);
        }
        sb.Append("</div>");

        sb.Append("<div class=card><h2>Your corrections</h2><table>");
        sb.Append($"<tr><td>Marked as fixed</td><td class=num>{user.Recurring.Count}</td></tr>");
        sb.Append($"<tr><td>Marked as not fixed</td><td class=num>{user.NotRecurring.Count}</td></tr>");
        sb.Append($"<tr><td>Category changes</td><td class=num>{user.Categories.Count}</td></tr>");
        sb.Append($"<tr><td>Renamed payments</td><td class=num>{user.Names.Count}</td></tr></table>");
        sb.Append("<div style='display:flex;gap:8px;margin-top:12px'><a class='btn ghost' href=/export.csv>Export CSV</a>");
        sb.Append("<button class='btn ghost' onclick=\"if(confirm('Forget all your corrections?'))rule('reset','')\">Reset corrections</button></div>");
        sb.Append("<p class=muted>Stored in data/user-rules.json next to the app. Keyword categories are in appsettings.json.</p></div></div>");
        return Layout("settings", month, a, s, sb.ToString());
    }

    // ================================================================== Bank picker

    public static string Banks(List<(string Name, string Country)> banks)
    {
        var sb = new StringBuilder($"<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Choose bank</title><style>{Css}</style></head><body><main style='max-width:640px'><h1>Choose your bank</h1><div class=card><table>");
        foreach (var b in banks.OrderByDescending(b => b.Name.Contains("KBC", StringComparison.OrdinalIgnoreCase)).ThenBy(b => b.Name))
            sb.Append($"<tr><td>{E(b.Name)}</td><td class=num><a class=btn href='/connect?name={Uri.EscapeDataString(b.Name)}&country={b.Country}'>Connect</a></td></tr>");
        sb.Append("</table></div><p><a href=/>← back</a></p></main></body></html>");
        return sb.ToString();
    }
}
