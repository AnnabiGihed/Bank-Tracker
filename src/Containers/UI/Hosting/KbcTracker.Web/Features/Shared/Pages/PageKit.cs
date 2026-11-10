using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace KbcTracker.Web.Features.Shared.Pages;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Holds what every page of the web host shares: the stylesheet, the page shell (header, month
///              selector, tabs, toast), the category picker, the fixed-payment status pill, and the
///              HTML, money and JSON formatting helpers. Pages import it with <c>using static</c>.
/// </summary>
internal static class PageKit
{
	#region Constants
	/// <summary>
	/// The stylesheet shared by every page, inlined so the app needs no static files.
	/// </summary>
	internal const string Css = """
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
	#endregion

	#region Static Instances
	/// <summary>
	/// Belgian French formatting for amounts (space as thousands separator, comma as decimal separator).
	/// </summary>
	internal static readonly CultureInfo Be = CultureInfo.GetCultureInfo("fr-BE");
	#endregion

	#region Page Layout
	/// <summary>
	/// Wraps a tab's body in the page shell: header, month selector, tabs, attention banner, toast and scripts.
	/// </summary>
	internal static string Layout(string tab, string month, Analysis a, SessionInfo? s, string body, string? script = null)
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
		AppendSyncNotices(sb, s);
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

	/// <summary>
	/// Renders an empty-state card with a message.
	/// </summary>
	internal static string Empty(string text) => $"<div class=card><div class=empty>{text}</div></div>";

	/// <summary>
	/// Renders a category drop-down grouped by category type; an unconfirmed category is shown dashed.
	/// </summary>
	internal static string CategorySelect(Analysis a, Category current, bool confirmed, string onchange)
	{
		var sb = new StringBuilder($"<select class='{(confirmed ? "" : "auto")}' title='{(confirmed ? "Confirmed" : "Automatic — not confirmed yet")}' onchange=\"{onchange}\">");
		foreach (var g in a.CategoryList.GroupBy(c => c.Type).OrderBy(g => g.Key))
		{
			sb.Append($"<optgroup label='{g.Key}'>");
			foreach (var c in g.OrderBy(c => c.Name)) sb.Append($"<option value='{Html(c.Id.Value)}'{(c.Id == current.Id ? " selected" : "")}>{Html(c.Name)}</option>");
			sb.Append("</optgroup>");
		}
		return sb.Append("</select>").ToString();
	}

	/// <summary>
	/// Renders the status of a fixed payment in the month as a coloured pill.
	/// </summary>
	internal static string StatusPill((FixedStatus Status, decimal Paid, DateOnly? Date) st, FixedRow f) => st.Status switch
	{
		FixedStatus.Paid => $"<span class='pill ok'>paid {st.Date:dd/MM}</span>",
		FixedStatus.PaidChanged => $"<span class='pill bad' title='Usually {Money(f.Def.Amount)}'>paid {Money(st.Paid)} (usually {WholeMoney(f.Def.Amount)})</span>",
		FixedStatus.Due => $"<span class='pill due'>due ~{st.Date:dd/MM}</span>",
		FixedStatus.Late => $"<span class='pill bad'>expected ~{st.Date:dd/MM}, not seen</span>",
		FixedStatus.NotThisMonth => "<span class='pill nil'>not this month</span>",
		_ => "<span class='pill nil'>—</span>"
	};

	/// <summary>
	/// Adds the banner of a failed sync, or, after a successful one, the number of transactions the bank sent that
	/// could not be read, with a link to sync the whole history again once that is fixed.
	/// </summary>
	private static void AppendSyncNotices(StringBuilder sb, SessionInfo? s)
	{
		if (s?.LastError is not null)
		{
			sb.Append(CultureInfo.InvariantCulture, $"<div class='notice err'><span><b>Last sync failed:</b> {Html(s.LastError)}</span><a class='btn sm' href=/banks>Reconnect</a></div>");
		}
		else if (s is { SkippedTransactions: > 0 })
		{
			sb.Append(CultureInfo.InvariantCulture, $"<div class='notice err'><span><b>Sync:</b> {s.SkippedTransactions} transaction(s) from the bank could not be read and were left out.</span><a class='btn sm' href='/sync?full=true&back=/settings'>Sync all again</a></div>");
		}
	}
	#endregion

	#region Formatting
	/// <summary>
	/// HTML-encodes text; <see langword="null"/> becomes empty.
	/// </summary>
	internal static string Html(string? s) => WebUtility.HtmlEncode(s ?? "");

	/// <summary>
	/// Formats an amount of money with cents.
	/// </summary>
	internal static string Money(decimal d) => d.ToString("#,##0.00", Be) + " €";

	/// <summary>
	/// Formats an amount of money without cents.
	/// </summary>
	internal static string WholeMoney(decimal d) => d.ToString("#,##0", Be) + " €";

	/// <summary>
	/// Formats a <c>yyyy-MM</c> month as its English name and year.
	/// </summary>
	internal static string MonthName(string m) =>
		DateTime.ParseExact(m + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));

	/// <summary>
	/// Serialises a value to JSON for inline scripts.
	/// </summary>
	internal static string Js(object? o) => JsonSerializer.Serialize(o);

	/// <summary>
	/// Serialises a value to JSON that is safe inside an HTML attribute.
	/// </summary>
	internal static string JsonAttribute(object? o) => Html(Js(o));

	/// <summary>
	/// Renders a category's colour dot.
	/// </summary>
	internal static string Dot(Category c) => $"<i class=dot style='background:{Html(c.Color)}'></i>";
	#endregion
}
