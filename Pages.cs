using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KbcTracker;

/// <summary>Server-rendered pages. One tab = one page, driven by the selected month (?m=yyyy-MM).</summary>
public static class Pages
{
    private static readonly CultureInfo Be = CultureInfo.GetCultureInfo("fr-BE");
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string M(decimal d) => d.ToString("#,##0.00", Be) + " €";
    private static string M0(decimal d) => d.ToString("#,##0", Be) + " €";
    private static string MonthName(string m) =>
        DateTime.ParseExact(m + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
    private static string Js(object? o) => JsonSerializer.Serialize(o);
    private static string A(object? o) => E(Js(o));   // JSON safe to put inside an HTML attribute
    private static string Dot(Category c) => $"<i class=dot style='background:{E(c.Color)}'></i>";

    // ================================================================== layout

    private const string Css = """
        :root{--bg:#f4f5f8;--card:#fff;--ink:#1c2230;--muted:#6a7386;--line:#e4e7ee;--soft:#f0f2f7;
              --brand:#3454d1;--fixed:#3454d1;--var:#f2994a;--inc:#27ae60;--save:#9b51e0;--bad:#e5484d;--warn:#e8890c}
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
        nav{max-width:1180px;margin:0 auto;padding:0 18px;display:flex;gap:2px;overflow-x:auto}
        nav a{padding:10px 13px;text-decoration:none;color:var(--muted);font-weight:600;border-bottom:2px solid transparent;white-space:nowrap}
        nav a.on{color:var(--brand);border-color:var(--brand)}
        nav b{background:var(--warn);color:#fff;border-radius:99px;font-size:11px;padding:1px 7px;margin-left:5px}
        main{max-width:1180px;margin:0 auto;padding:18px}
        .grid{display:grid;gap:12px}.k5{grid-template-columns:repeat(5,1fr)}.c2{grid-template-columns:3fr 2fr}.c2e{grid-template-columns:1fr 1fr}.c3{grid-template-columns:repeat(3,1fr)}
        @media(max-width:900px){.k5{grid-template-columns:repeat(2,1fr)}.c2,.c2e,.c3{grid-template-columns:1fr}}
        .card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;min-width:0}
        .card h2{font-size:13px;margin:0 0 10px;color:var(--muted);text-transform:uppercase;letter-spacing:.04em;display:flex;justify-content:space-between;align-items:center;gap:8px}
        .kpi .v{font-size:22px;font-weight:700;margin-top:2px;font-variant-numeric:tabular-nums}.kpi .s{color:var(--muted);font-size:12px}
        .kpi{border-top:3px solid var(--line)}.kpi.inc{border-top-color:var(--inc)}.kpi.fix{border-top-color:var(--fixed)}.kpi.var{border-top-color:var(--var)}.kpi.sav{border-top-color:var(--save)}
        table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:8px 6px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{color:var(--muted);font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.04em;position:sticky;top:0;background:var(--card);z-index:1}
        tr:last-child td{border-bottom:0}
        .num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
        .muted{color:var(--muted);font-size:12px}.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:420px}
        .pill{display:inline-block;padding:2px 9px;border-radius:99px;font-size:12px;font-weight:600;white-space:nowrap}
        .ok{background:#27ae6020;color:var(--inc)}.due{background:#f2994a22;color:var(--warn)}.bad{background:#e5484d1f;color:var(--bad)}.nil{background:var(--soft);color:var(--muted)}
        .tag{display:inline-flex;align-items:center;gap:5px;padding:1px 7px;border-radius:6px;font-size:11px;background:var(--soft);color:var(--muted);margin-left:6px;white-space:nowrap}
        .dot{display:inline-block;width:9px;height:9px;border-radius:50%;flex:none}
        .btn{display:inline-flex;align-items:center;gap:6px;background:var(--brand);color:#fff;border:0;border-radius:8px;padding:7px 12px;text-decoration:none;font:inherit;font-weight:600;cursor:pointer;white-space:nowrap}
        .btn.ghost{background:transparent;color:var(--ink);border:1px solid var(--line)}.btn.sm{padding:4px 9px;font-size:12px}
        .btn.okb{background:var(--inc)}
        .icon{border:0;background:transparent;cursor:pointer;color:var(--muted);font-size:16px;padding:2px 6px;border-radius:6px}.icon:hover{background:var(--soft);color:var(--ink)}
        .icon.on{color:#f2b01e}
        .scroll{max-height:calc(100vh - 250px);overflow:auto}
        .chart{position:relative;height:220px}
        .filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px;align-items:center}
        input,select,textarea{border:1px solid var(--line);background:var(--card);color:var(--ink);border-radius:8px;padding:6px 8px;font:inherit}
        td select{padding:3px 6px;font-size:12px;max-width:180px}
        select.auto{border-style:dashed;color:var(--muted)}
        .bar{height:8px;border-radius:4px;background:var(--soft);overflow:hidden}.bar i{display:block;height:100%}
        .stat{display:flex;gap:28px;flex-wrap:wrap}.stat div b{display:block;font-size:19px}
        .empty{padding:30px;text-align:center;color:var(--muted)}
        .notice{display:flex;gap:10px;align-items:center;justify-content:space-between;background:#f2994a18;border:1px solid #f2994a55;border-radius:12px;padding:10px 14px;margin-bottom:12px}
        .notice.err{background:#e5484d14;border-color:#e5484d55}
        .sug{border-left:4px solid var(--warn)}.sug .amt{font-size:20px;font-weight:700}
        .chips{display:flex;gap:4px;flex-wrap:wrap;margin:8px 0}.chips span{background:var(--soft);border-radius:6px;padding:1px 6px;font-size:11px;color:var(--muted)}
        dialog{border:1px solid var(--line);border-radius:14px;background:var(--card);color:var(--ink);padding:18px;width:min(460px,92vw)}
        dialog::backdrop{background:#0006}
        dialog label{display:block;font-size:12px;color:var(--muted);margin:10px 0 4px}dialog input,dialog select,dialog textarea{width:100%}
        #toast{position:fixed;bottom:18px;left:50%;transform:translateX(-50%);background:var(--ink);color:var(--bg);padding:10px 14px;border-radius:10px;display:none;gap:12px;align-items:center;z-index:20;box-shadow:0 6px 24px #0004}
        #toast button{background:transparent;color:inherit;border:1px solid currentColor;border-radius:6px;padding:3px 8px;cursor:pointer;font:inherit}
        """;

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
            if (a.Items.Count > 0) sb.Append($" · history since {a.Items.Min(i => i.T.Date):dd/MM/yyyy}");
            if (left < 15) sb.Append($" · <b style='color:var(--bad)'>bank access ends in {left} days</b>");
        }
        sb.Append("</div>");
        if (tab is not ("settings" or "review"))
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
        var tabs = new (string Id, string Label, int Badge)[]
        {
            ("overview", "Overview", 0), ("fixed", "Fixed payments", a.Suggestions.Count), ("transactions", "Transactions", 0),
            ("review", "To review", a.Review.Count), ("categories", "Categories", 0), ("settings", "Settings", 0)
        };
        foreach (var (id, label, badge) in tabs)
            sb.Append($"<a class='{(id == tab ? "on" : "")}' href='/{id}?m={month}'>{label}{(badge > 0 ? $"<b>{badge}</b>" : "")}</a>");
        sb.Append("</nav></header><main>");
        if (s?.LastError is not null)
            sb.Append($"<div class='notice err'><span><b>Last sync failed:</b> {E(s.LastError)}</span><a class='btn sm' href=/banks>Reconnect</a></div>");
        sb.Append(body).Append("</main><div id=toast></div>");
        sb.Append("""
            <script>
            async function api(body,reload=true){
              const r=await fetch('/api',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
              if(!r.ok){alert(await r.text()||'Something went wrong');return null}
              const t=await r.text();const j=t?JSON.parse(t):null;
              if(reload)location.reload();return j;
            }
            function toast(html,ms=7000){const t=document.getElementById('toast');t.innerHTML=html;t.style.display='flex';clearTimeout(window._tt);window._tt=setTimeout(()=>t.style.display='none',ms)}
            const css=n=>getComputedStyle(document.documentElement).getPropertyValue(n).trim();
            if(window.Chart){Chart.defaults.color=css('--muted');Chart.defaults.borderColor=css('--line');Chart.defaults.font.family='system-ui'}
            const eur=v=>v.toLocaleString('fr-BE',{minimumFractionDigits:2,maximumFractionDigits:2})+' €';
            </script>
            """);
        if (script is not null) sb.Append("<script>").Append(script).Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Empty(string text) => $"<div class=card><div class=empty>{text}</div></div>";

    private static string CategorySelect(Analysis a, Category current, bool confirmed, string onchange)
    {
        var sb = new StringBuilder($"<select class='{(confirmed ? "" : "auto")}' title='{(confirmed ? "Confirmed" : "Automatic — not confirmed yet")}' onchange=\"{onchange}\">");
        foreach (var g in a.CategoryList.GroupBy(c => c.Type).OrderBy(g => g.Key))
        {
            sb.Append($"<optgroup label='{g.Key}'>");
            foreach (var c in g.OrderBy(c => c.Name)) sb.Append($"<option value='{E(c.Id)}'{(c.Id == current.Id ? " selected" : "")}>{E(c.Name)}</option>");
            sb.Append("</optgroup>");
        }
        return sb.Append("</select>").ToString();
    }

    // ================================================================== Overview

    public static string Overview(Analysis a, string month, SessionInfo? s)
    {
        if (a.Items.Count == 0)
            return Layout("overview", month, a, s, Empty(s is null
                ? "Connect your bank to get started: click <b>Connect bank</b> and log in with itsme."
                : "No transactions yet — click <b>↻ Sync</b>."));

        var cur = a.MonthData(month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fixedRows = a.Fixed.Select(f => (f, st: f.StatusIn(month, today))).Where(x => x.st.Status is not (FixedStatus.NotStarted or FixedStatus.NotThisMonth) || x.st.Paid > 0).ToList();
        var open = fixedRows.Where(x => x.st.Status is FixedStatus.Due or FixedStatus.Late).ToList();

        var sb = new StringBuilder();
        if (a.Suggestions.Count > 0 || a.Review.Count > 0)
        {
            sb.Append("<div class=notice><span>");
            if (a.Suggestions.Count > 0) sb.Append($"<b>{a.Suggestions.Count}</b> possible fixed payment(s) found. ");
            if (a.Review.Count > 0) sb.Append($"<b>{a.Review.Count}</b> payee(s) with an automatic category to confirm.");
            sb.Append("</span><span style='display:flex;gap:6px'>");
            if (a.Suggestions.Count > 0) sb.Append($"<a class='btn sm' href='/fixed?m={month}'>Check fixed payments</a>");
            if (a.Review.Count > 0) sb.Append($"<a class='btn sm ghost' href='/review?m={month}'>Review categories</a>");
            sb.Append("</span></div>");
        }

        sb.Append("<div class='grid k5' style='margin-bottom:12px'>");
        Kpi(sb, "inc", "Income", M(cur.Income), "salary, refunds…");
        Kpi(sb, "", "Spent", M(cur.Spent), $"fixed {M0(cur.Fixed)} · variable {M0(cur.Variable)}");
        Kpi(sb, "fix", "Fixed payments", M(cur.Fixed), open.Count == 0 ? (fixedRows.Count == 0 ? "none confirmed yet" : "all paid") : $"{open.Count} still to come · {M0(open.Sum(x => x.f.Def.Amount))}");
        Kpi(sb, "sav", "Saved & invested", M(cur.Saved), "plans, spare change…");
        Kpi(sb, "var", "Left over", M(cur.Net), "income − spent − saved");
        sb.Append("</div>");

        sb.Append("<div class='grid c2' style='margin-bottom:12px'>");
        sb.Append("<div class=card><h2>Last months</h2><div class=chart><canvas id=months></canvas></div></div>");
        sb.Append($"<div class=card><h2>Where it went</h2><div class=chart><canvas id=cats></canvas></div></div></div>");

        sb.Append("<div class='grid c2e'>");
        sb.Append($"<div class=card><h2>Fixed payments <a class=muted href='/fixed?m={month}'>see all →</a></h2><table>");
        foreach (var (f, st) in fixedRows.OrderBy(x => x.st.Status is FixedStatus.Paid or FixedStatus.PaidChanged).ThenBy(x => x.f.UsualDay).Take(6))
            sb.Append($"<tr><td>{E(f.Def.Name)}</td><td class=num>{M(st.Paid > 0 ? st.Paid : f.Def.Amount)}</td><td style='width:1%'>{StatusPill(st, f)}</td></tr>");
        if (fixedRows.Count == 0) sb.Append($"<tr><td class=muted>No confirmed fixed payments yet{(a.Suggestions.Count > 0 ? $" — <a href='/fixed?m={month}'>{a.Suggestions.Count} suggestions waiting</a>" : "")}.</td></tr>");
        sb.Append($"</table><div class=muted style='margin-top:8px'>{fixedRows.Count(x => x.st.Paid > 0)} of {fixedRows.Count} paid this month</div></div>");

        sb.Append($"<div class=card><h2>Biggest expenses <a class=muted href='/transactions?m={month}'>all transactions →</a></h2><table>");
        foreach (var i in a.Items.Where(i => i.Month == month && i.IsSpending).OrderBy(i => i.T.Amount).Take(6))
            sb.Append($"<tr><td class=muted style='width:1%'>{i.T.Date:dd/MM}</td><td>{E(i.Name)}<span class=tag>{Dot(i.Category)}{E(i.Category.Name)}</span></td><td class=num>{M(-i.T.Amount)}</td></tr>");
        sb.Append("</table></div></div>");

        var months = a.Months.TakeLast(12).ToList();
        var cats = cur.ByCategory.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => (c: a.CategoryById(kv.Key), v: kv.Value)).ToList();
        var data = Js(new
        {
            l = months.Select(m => MonthName(m.Month)[..3] + " " + m.Month[2..4]),
            f = months.Select(m => m.Fixed), v = months.Select(m => m.Variable), s = months.Select(m => m.Saved), i = months.Select(m => m.Income),
            cl = cats.Select(c => c.c.Name), ci = cats.Select(c => c.c.Id), cc = cats.Select(c => c.c.Color), cv = cats.Select(c => c.v)
        });
        var script = """
            const d=__D__;
            new Chart(document.getElementById('months'),{type:'bar',data:{labels:d.l,datasets:[
              {label:'Fixed',data:d.f,backgroundColor:css('--fixed'),stack:'s',borderRadius:4},
              {label:'Variable',data:d.v,backgroundColor:css('--var'),stack:'s',borderRadius:4},
              {label:'Saved',data:d.s,backgroundColor:css('--save'),stack:'s',borderRadius:4},
              {label:'Income',data:d.i,type:'line',borderColor:css('--inc'),backgroundColor:css('--inc'),tension:.3,pointRadius:3}]},
              options:{maintainAspectRatio:false,interaction:{mode:'index',intersect:false},plugins:{legend:{position:'bottom'},tooltip:{callbacks:{label:c=>`${c.dataset.label}: ${eur(c.raw)}`}}},scales:{y:{ticks:{callback:v=>v.toLocaleString('fr-BE')+' €'}}}}});
            new Chart(document.getElementById('cats'),{type:'doughnut',data:{labels:d.cl,datasets:[{data:d.cv,backgroundColor:d.cc,borderWidth:0}]},
              options:{maintainAspectRatio:false,cutout:'62%',plugins:{legend:{position:'right',labels:{boxWidth:10}},tooltip:{callbacks:{label:c=>`${c.label}: ${eur(c.raw)}`}}},
              onClick:(e,el)=>{if(el.length)location.href='/transactions?m=__M__&cat='+encodeURIComponent(d.ci[el[0].index])}}});
            """.Replace("__D__", data).Replace("__M__", month);
        return Layout("overview", month, a, s, sb.ToString(), script);
    }

    private static void Kpi(StringBuilder sb, string cls, string label, string value, string sub) =>
        sb.Append($"<div class='card kpi {cls}'><div class=muted>{E(label)}</div><div class=v>{value}</div><div class=s>{E(sub)}</div></div>");

    private static string StatusPill((FixedStatus Status, decimal Paid, DateOnly? Date) st, FixedRow f) => st.Status switch
    {
        FixedStatus.Paid => $"<span class='pill ok'>paid {st.Date:dd/MM}</span>",
        FixedStatus.PaidChanged => $"<span class='pill bad' title='Usually {M(f.Def.Amount)}'>paid {M(st.Paid)} (usually {M0(f.Def.Amount)})</span>",
        FixedStatus.Due => $"<span class='pill due'>due ~{st.Date:dd/MM}</span>",
        FixedStatus.Late => $"<span class='pill bad'>expected ~{st.Date:dd/MM}, not seen</span>",
        FixedStatus.NotThisMonth => "<span class='pill nil'>not this month</span>",
        _ => "<span class='pill nil'>—</span>"
    };

    // ================================================================== Fixed payments

    public static string Fixed(Analysis a, string month, SessionInfo? s)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var sb = new StringBuilder();

        if (a.Suggestions.Count > 0)
        {
            sb.Append($"<div class=card style='margin-bottom:12px'><h2>Possible fixed payments — please check <span class=muted style='text-transform:none'>accept to start tracking them</span></h2><div class='grid c3'>");
            foreach (var g in a.Suggestions)
            {
                var cat = a.CategoryById(g.CategoryId);
                sb.Append($"""
                    <div class='card sug'>
                      <div style='display:flex;justify-content:space-between;gap:8px'><b>{E(g.Name)}</b>{(g.HighConfidence ? "" : "<span class='pill nil' title='Only 2 payments seen so far'>maybe</span>")}</div>
                      <div class=amt>{M(g.Amount)} <span class=muted>{g.Cadence.ToLowerInvariant()}</span></div>
                      <div class=muted>{E(g.Reason)} · <span class=tag style='margin:0'>{Dot(cat)}{E(cat.Name)}</span></div>
                      <div class=chips>{string.Concat(g.Items.TakeLast(6).Select(i => $"<span title='{E(i.T.Description)}'>{i.T.Date:dd/MM} · {M(-i.T.Amount)}</span>"))}</div>
                      <div style='display:flex;gap:6px'>
                        <button class='btn sm okb' onclick="api({A(new { action = "accept", key = g.Signature })})">✓ Fixed payment</button>
                        <button class='btn sm ghost' onclick="api({A(new { action = "dismiss", key = g.Signature })})">✕ Not fixed</button>
                      </div>
                    </div>
                    """);
            }
            sb.Append("</div></div>");
        }

        var rows = a.Fixed.Select(f => (f, st: f.StatusIn(month, today))).Where(x => x.st.Status != FixedStatus.NotStarted).ToList();
        var monthly = rows.Where(x => x.st.Status != FixedStatus.NotThisMonth || x.st.Paid > 0).ToList();
        sb.Append("<div class=card style='margin-bottom:12px'><div class=stat>");
        sb.Append($"<div><span class=muted>Expected in {MonthName(month)}</span><b>{M(monthly.Sum(x => x.st.Paid > 0 ? x.st.Paid : x.f.Def.Amount))}</b></div>");
        sb.Append($"<div><span class=muted>Paid</span><b style='color:var(--inc)'>{M(monthly.Sum(x => x.st.Paid))}</b></div>");
        sb.Append($"<div><span class=muted>Still to come</span><b style='color:var(--warn)'>{M(monthly.Where(x => x.st.Status is FixedStatus.Due or FixedStatus.Late).Sum(x => x.f.Def.Amount))}</b></div>");
        sb.Append($"<div><span class=muted>Fixed payments</span><b>{a.Fixed.Count}</b></div></div></div>");

        sb.Append("<div class=card><div class=scroll><table><tr><th class=num>Day</th><th>Payment</th><th>Category</th><th class=num>Amount</th><th>This month</th><th></th></tr>");
        foreach (var (f, st) in rows.OrderBy(x => x.f.UsualDay))
        {
            var edit = new { f.Def.Id, f.Def.Name, f.Def.Amount, Cadence = f.Def.CadenceDays, CategoryId = f.Category.Id };
            sb.Append($"""
                <tr><td class=num>{f.UsualDay}</td>
                <td><b>{E(f.Def.Name)}</b><div class=muted>{Analysis.CadenceName(f.Def.CadenceDays)} · {f.Payments.Count} payment(s) · last {f.LastPaid:dd/MM/yy}</div></td>
                <td><span class=tag style='margin:0'>{Dot(f.Category)}{E(f.Category.Name)}</span></td><td class=num>{M(f.Def.Amount)}</td><td>{StatusPill(st, f)}</td>
                <td class=num><button class=icon title='Edit' onclick="editFixed({A(edit)})">✎</button><button class=icon title='Stop tracking' onclick="if(confirm('Stop tracking this fixed payment?'))api({A(new { action = "removeFixed", id = f.Def.Id })})">✕</button></td></tr>
                """);
        }
        if (rows.Count == 0) sb.Append("<tr><td colspan=6 class=empty>No confirmed fixed payments yet. Accept the suggestions above, or click ☆ next to a payment in Transactions.</td></tr>");
        sb.Append("</table></div><div class=muted style='margin-top:10px'>Missing one? In <b>Transactions</b> click ☆ next to it.</div></div>");

        sb.Append($"""
            <dialog id=fx><form method=dialog onsubmit="saveFixed();return false">
              <h3 style='margin:0'>Edit fixed payment</h3>
              <label>Name</label><input id=fxName required>
              <label>Category</label>{CategorySelect(a, a.CategoryById("other"), true, "").Replace("<select", "<select id=fxCat")}
              <label>Usual amount (€)</label><input id=fxAmount type=number step=0.01 min=0>
              <label>Rhythm</label><select id=fxCad><option value=30>Monthly</option><option value=91>Quarterly</option><option value=365>Yearly</option></select>
              <div style='display:flex;gap:8px;justify-content:flex-end;margin-top:14px'><button type=button class='btn ghost' onclick=fx.close()>Cancel</button><button class=btn>Save</button></div>
            </form></dialog>
            """);
        const string script = """
            let fxId=null;
            function editFixed(f){fxId=f.Id;fxName.value=f.Name;fxCat.value=f.CategoryId;fxAmount.value=f.Amount;fxCad.value=f.Cadence>200?365:f.Cadence>40?91:30;fx.showModal()}
            function saveFixed(){api({action:'updateFixed',id:fxId,name:fxName.value,categoryId:fxCat.value,amount:parseFloat(fxAmount.value),cadence:parseInt(fxCad.value)})}
            """;
        return Layout("fixed", month, a, s, sb.ToString(), script);
    }

    // ================================================================== Transactions

    public static string Transactions(Analysis a, string month, SessionInfo? s, string? cat, string? q)
    {
        var items = a.Items.Where(i => i.Month == month).ToList();
        var sb = new StringBuilder("<div class=filters>");
        sb.Append($"<input id=q placeholder='Search name, text or amount…' value='{E(q)}' style='flex:1;min-width:200px'>");
        sb.Append("<select id=type><option value=spend>Spending</option><option value=income>Income</option><option value=save>Savings</option><option value=own>Own transfers</option><option value=all>Everything</option></select>");
        sb.Append("<select id=cat><option value=''>All categories</option>");
        foreach (var c in items.Select(i => i.Category).DistinctBy(c => c.Id).OrderBy(c => c.Name))
            sb.Append($"<option value='{E(c.Id)}'{(c.Id == cat ? " selected" : "")}>{E(c.Name)}</option>");
        sb.Append("</select><label class=muted><input type=checkbox id=unconf> not confirmed only</label></div>");

        sb.Append("<div class=card><div class=scroll><table id=tx><tr><th>Date</th><th>Name</th><th>Category</th><th class=num>Amount</th><th title='Fixed payment'>Fixed</th></tr>");
        foreach (var i in items)
        {
            var type = i.IsOwnMove ? "own" : i.IsSaving ? "save" : i.T.Amount > 0 ? "income" : "spend";
            var search = $"{i.Name} {i.T.Description} {Math.Abs(i.T.Amount).ToString("0.00", Be)}".ToLowerInvariant();
            var ctx = A(new { key = i.SeriesKey, id = i.T.Id, name = i.Name });
            sb.Append($"<tr data-type={type} data-cat='{E(i.Category.Id)}' data-conf={(i.CategoryConfirmed ? 1 : 0)} data-s='{E(search)}' data-a='{(-i.T.Amount).ToString(CultureInfo.InvariantCulture)}'>");
            sb.Append($"<td class=muted style='width:1%'>{i.T.Date:dd/MM}</td>");
            sb.Append($"<td><b>{E(i.Name)}</b><div class='muted ell' title='{E(i.T.Description)}'>{E(i.T.Description)}</div></td>");
            sb.Append("<td style='white-space:nowrap'>").Append(CategorySelect(a, i.Category, i.CategoryConfirmed, $"setCat({ctx},this)"));
            if (!i.CategoryConfirmed) sb.Append($"<button class=icon title='Confirm this category for {E(i.Name)}' onclick=\"api({A(new { action = "confirm", key = i.SeriesKey })})\">✓</button>");
            sb.Append("</td>");
            sb.Append($"<td class=num style='color:{(i.T.Amount > 0 ? "var(--inc)" : "inherit")}'>{M(i.T.Amount)}</td>");
            sb.Append(i.T.Amount >= 0 || i.IsOwnMove ? "<td></td>"
                : i.Recurring is not null
                    ? $"<td style='width:1%'><a class='icon on' title='Fixed payment: {E(i.Recurring.Name)}' href='/fixed?m={month}' style='text-decoration:none'>★</a></td>"
                    : $"<td style='width:1%'><button class=icon title='Mark {E(i.Name)} as a fixed payment' onclick=\"api({A(new { action = "mark", key = i.SeriesKey })})\">☆</button></td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        if (items.Count == 0) sb.Append("<div class=empty>No transactions in this month.</div>");
        sb.Append("</div><div class=muted style='margin-top:10px;display:flex;justify-content:space-between'><span id=count></span><b id=total></b></div></div>");

        const string script = """
            const q=document.getElementById('q'),type=document.getElementById('type'),cat=document.getElementById('cat'),unconf=document.getElementById('unconf');
            if(cat.value||q.value)type.value='all';
            function apply(){
              let n=0,t=0;const s=q.value.trim().toLowerCase();
              document.querySelectorAll('#tx tr[data-type]').forEach(r=>{
                const ok=(type.value==='all'||r.dataset.type===type.value)&&(!cat.value||r.dataset.cat===cat.value)&&(!s||r.dataset.s.includes(s))&&(!unconf.checked||r.dataset.conf==='0');
                r.style.display=ok?'':'none';if(ok){n++;t+=parseFloat(r.dataset.a)}});
              document.getElementById('count').textContent=n+' transactions';
              document.getElementById('total').textContent=(t>=0?'Total out: ':'Total in: ')+eur(Math.abs(t));
            }
            [q,type,cat,unconf].forEach(e=>e.addEventListener('input',apply));apply();
            // category change applies to the whole payee; the toast offers "only this one"
            async function setCat(x,sel){
              const prev=await api({action:'payeeCategory',key:x.key,categoryId:sel.value},false);
              sel.classList.remove('auto');
              const label=sel.options[sel.selectedIndex].text;
              window._undo={x,prev,cat:sel.value};
              toast(`<span>“${label}” set for all payments to <b>${x.name}</b></span><button onclick="onlyThis()">Only this one</button><button onclick="location.reload()">OK</button>`);
            }
            async function onlyThis(){const u=window._undo;await api({action:'txnOnly',key:u.x.key,id:u.x.id,categoryId:u.cat,restore:u.prev})}
            """;
        return Layout("transactions", month, a, s, sb.ToString(), script);
    }

    // ================================================================== To review

    public static string ReviewPage(Analysis a, string month, SessionInfo? s)
    {
        var sb = new StringBuilder();
        if (a.Review.Count == 0)
            return Layout("review", month, a, s, Empty("All categories are confirmed. 🎉 New payees will show up here after a sync."));

        sb.Append($"<div class=filters><span class=muted style='margin-right:auto'>These payees got a category automatically. Confirm it (✓) or pick another — it then applies to all their payments, past and future.</span>");
        sb.Append("<select id=dir><option value=all>All</option><option value=out>Money out</option><option value=in>Money in</option></select>");
        sb.Append("<button class='btn okb' onclick=confirmVisible()>✓ Confirm all shown</button></div>");
        sb.Append("<div class=card><div class=scroll><table id=rv><tr><th>Payee</th><th class=num>Payments</th><th class=num>Total</th><th class=num>Last</th><th>Category</th><th></th></tr>");
        foreach (var r in a.Review)
        {
            var ctx = A(new { key = r.SeriesKey });
            sb.Append($"<tr data-dir={(r.Incoming ? "in" : "out")} data-key='{E(r.SeriesKey)}'>");
            sb.Append($"<td><b>{E(r.Name)}</b><div class='muted ell' title='{E(r.Sample)}'>{E(r.Sample)}</div></td>");
            sb.Append($"<td class=num>{r.Count}</td><td class=num style='color:{(r.Total > 0 ? "var(--inc)" : "inherit")}'>{M(r.Total)}</td><td class='num muted'>{r.Last:dd/MM/yy}</td>");
            sb.Append("<td>").Append(CategorySelect(a, r.Category, false, $"api({{action:'payeeCategory',key:{ctx}.key,categoryId:this.value}})")).Append("</td>");
            sb.Append($"<td class=num><button class='btn sm okb' onclick=\"api({A(new { action = "confirm", key = r.SeriesKey })})\">✓</button></td></tr>");
        }
        sb.Append("</table></div></div>");
        const string script = """
            const dir=document.getElementById('dir');
            dir.addEventListener('input',()=>document.querySelectorAll('#rv tr[data-dir]').forEach(r=>r.style.display=dir.value==='all'||r.dataset.dir===dir.value?'':'none'));
            function confirmVisible(){
              const keys=[...document.querySelectorAll('#rv tr[data-dir]')].filter(r=>r.style.display!=='none').map(r=>r.dataset.key);
              if(confirm(`Confirm the category of ${keys.length} payees?`))api({action:'confirm',keys});
            }
            """;
        return Layout("review", month, a, s, sb.ToString(), script);
    }

    // ================================================================== Categories

    public static string CategoriesPage(Analysis a, string month, SessionInfo? s)
    {
        var inMonth = a.Items.Where(i => i.Month == month).ToList();
        var prevMonths = a.AvailableMonths.Where(m => string.CompareOrdinal(m, month) < 0).Take(3).ToList();
        decimal Total(Category c, IEnumerable<TxnInfo> items) =>
            c.Type == CategoryType.Income ? items.Where(i => i.Category.Id == c.Id).Sum(i => i.T.Amount)
                                          : items.Where(i => i.Category.Id == c.Id).Sum(i => -i.T.Amount);

        var sb = new StringBuilder($"<div class=filters><span class=muted style='margin-right:auto'>{MonthName(month)} · click a category to see its transactions</span><button class=btn onclick=\"editCat(null)\">+ New category</button></div>");
        foreach (var group in a.CategoryList.GroupBy(c => c.Type).OrderBy(g => g.Key))
        {
            var rows = group.Select(c => (c, v: Total(c, inMonth),
                                          avg: prevMonths.Count == 0 ? (decimal?)null : prevMonths.Average(m => Total(c, a.Items.Where(i => i.Month == m))),
                                          n: a.Items.Count(i => i.Category.Id == c.Id)))
                            .OrderByDescending(x => x.v).ThenBy(x => x.c.Name).ToList();
            var max = rows.Select(r => r.v).DefaultIfEmpty(0).Max();
            var title = group.Key switch { CategoryType.Expense => "Spending", CategoryType.Income => "Income", CategoryType.Savings => "Savings", _ => "Own transfers (not counted)" };
            sb.Append($"<div class=card style='margin-bottom:12px'><h2>{title}<span>{M(rows.Sum(r => r.v))}</span></h2><table><tr><th>Category</th><th style='width:30%'></th><th class=num>{MonthName(month)}</th><th class=num>vs. previous months</th><th>Keywords</th><th></th></tr>");
            foreach (var (c, v, avg, n) in rows)
            {
                var diff = avg is null || avg == 0 || group.Key != CategoryType.Expense ? "<span class=muted>—</span>"
                    : v > avg * 1.15m ? $"<span class='pill bad'>+{M0(v - avg.Value)}</span>"
                    : v < avg * 0.85m ? $"<span class='pill ok'>−{M0(avg.Value - v)}</span>"
                    : "<span class='pill nil'>similar</span>";
                var edit = new { c.Id, c.Name, Type = c.Type.ToString(), c.Color, Keywords = string.Join(", ", c.Keywords), c.BuiltIn };
                sb.Append($"<tr><td style='cursor:pointer' onclick=\"location.href='/transactions?m={month}&cat={Uri.EscapeDataString(c.Id)}'\"><b style='display:flex;align-items:center;gap:8px'>{Dot(c)}{E(c.Name)}</b><div class=muted>{n} payment(s) in total</div></td>");
                sb.Append($"<td><div class=bar><i style='width:{(max <= 0 || v <= 0 ? 0 : v / max * 100):0}%;background:{E(c.Color)}'></i></div></td>");
                sb.Append($"<td class=num>{(v == 0 ? "<span class=muted>—</span>" : M(v))}</td><td class=num>{diff}</td>");
                sb.Append($"<td class='muted ell' style='max-width:220px'>{E(string.Join(", ", c.Keywords))}</td>");
                sb.Append($"<td class=num><button class=icon title='Edit' onclick=\"editCat({A(edit)})\">✎</button></td></tr>");
            }
            sb.Append("</table></div>");
        }

        sb.Append("""
            <dialog id=cd><form method=dialog onsubmit="saveCat();return false">
              <h3 id=cdTitle style='margin:0'>Category</h3>
              <label>Name</label><input id=cdName required>
              <label>Counts as</label><select id=cdType>
                <option value=Expense>Spending</option><option value=Income>Income</option>
                <option value=Savings>Savings (not spending)</option><option value=Transfer>Own transfer (ignored)</option></select>
              <label>Colour</label><input id=cdColor type=color style='height:36px;padding:2px'>
              <label>Keywords — payments whose shop/payee name contains one of these get this category automatically (comma separated)</label>
              <textarea id=cdKw rows=3 placeholder='e.g. DELHAIZE, COLRUYT, ALDI'></textarea>
              <div style='display:flex;gap:8px;justify-content:space-between;margin-top:14px'>
                <button type=button id=cdDel class='btn ghost' style='color:var(--bad)' onclick=delCat()>Delete</button>
                <span style='display:flex;gap:8px'><button type=button class='btn ghost' onclick=cd.close()>Cancel</button><button class=btn>Save</button></span>
              </div>
            </form></dialog>
            """);
        const string script = """
            let cdId=null;
            function editCat(c){cdId=c?.Id??null;cdTitle.textContent=c?'Edit category':'New category';cdName.value=c?.Name??'';cdType.value=c?.Type??'Expense';
              cdColor.value=c?.Color??'#3454d1';cdKw.value=c?.Keywords??'';cdDel.style.visibility=c&&c.Id!=='other'?'visible':'hidden';cd.showModal()}
            function saveCat(){api({action:'saveCategory',id:cdId,name:cdName.value,type:cdType.value,color:cdColor.value,keywords:cdKw.value})}
            function delCat(){if(confirm('Delete this category? Its payments go back to “Other” (to review).'))api({action:'deleteCategory',id:cdId})}
            """;
        return Layout("categories", month, a, s, sb.ToString(), script);
    }

    // ================================================================== Settings

    public static string Settings(Analysis a, string month, SessionInfo? s, string dataDir)
    {
        var sb = new StringBuilder("<div class='grid c2e'>");
        sb.Append("<div class=card><h2>Bank connection</h2>");
        if (s is null) sb.Append("<p>Not connected.</p><a class=btn href=/banks>Connect KBC Brussels</a>");
        else
        {
            sb.Append($"""
                <table>
                <tr><td class=muted>Accounts</td><td>{s.AccountUids.Count}</td></tr>
                <tr><td class=muted>Bank access valid until</td><td>{s.ValidUntil.ToLocalTime():dd/MM/yyyy} <span class=muted>(then click Reconnect)</span></td></tr>
                <tr><td class=muted>Last sync</td><td>{s.LastSync?.ToLocalTime():dd/MM/yyyy HH:mm}{(s.LastError is null ? "" : $" <span class='pill bad'>failed</span>")}</td></tr>
                </table>
                <div style='display:flex;gap:8px;flex-wrap:wrap;margin-top:12px'>
                  <a class=btn href='/sync?back=/settings'>↻ Sync now</a>
                  <a class='btn ghost' href=/banks>Reconnect / add account</a>
                </div>
                """);
        }
        sb.Append("</div>");

        sb.Append("<div class=card><h2>Your history</h2><table>");
        sb.Append($"<tr><td class=muted>Stored</td><td>{(a.Items.Count == 0 ? "—" : $"{a.Items.Min(i => i.T.Date):dd/MM/yyyy} → {a.Items.Max(i => i.T.Date):dd/MM/yyyy}")}</td></tr>");
        sb.Append($"<tr><td class=muted>Transactions</td><td>{a.Items.Count}</td></tr>");
        sb.Append($"<tr><td class=muted>Fixed payments</td><td>{a.Fixed.Count}</td></tr>");
        sb.Append($"<tr><td class=muted>Data folder</td><td><code style='font-size:12px'>{E(dataDir)}</code></td></tr></table>");
        sb.Append("""
            <p class=muted>KBC only shares the last ~90 days. This app keeps every transaction it has ever downloaded (with daily backups in the
            <i>backups</i> folder), so your history grows independently from KBC. While the app is running it syncs every 6 hours;
            run <code>scripts\install-daily-sync.ps1</code> once to also sync daily when it is closed.</p>
            <a class='btn ghost' href=/export.csv>Export CSV</a>
            """);
        sb.Append("</div></div>");
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
