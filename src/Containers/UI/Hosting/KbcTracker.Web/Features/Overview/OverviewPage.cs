using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Overview tab: the month's income, spending, savings, what is left, the last months, spending by
///              category, fixed payments still to come and the biggest expenses.
/// </summary>
public static class OverviewPage
{
	#region Public Methods
	/// <summary>
	/// Renders the Overview tab for a month: income, spending (fixed and variable), savings, what is left, the
	/// last months, spending by category, fixed payments still to come and the biggest expenses.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s)
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
		Kpi(sb, "inc", "Income", Money(cur.Income), "salary, refunds…");
		Kpi(sb, "", "Spent", Money(cur.Spent), $"fixed {WholeMoney(cur.Fixed)} · variable {WholeMoney(cur.Variable)}");
		Kpi(sb, "fix", "Fixed payments", Money(cur.Fixed), open.Count == 0 ? (fixedRows.Count == 0 ? "none confirmed yet" : "all paid") : $"{open.Count} still to come · {WholeMoney(open.Sum(x => x.f.Def.Amount))}");
		Kpi(sb, "sav", "Saved & invested", Money(cur.Saved), "plans, spare change…");
		Kpi(sb, "var", "Left over", Money(cur.Net), "income − spent − saved");
		sb.Append("</div>");

		sb.Append("<div class='grid c2' style='margin-bottom:12px'>");
		sb.Append("<div class=card><h2>Last months</h2><div class=chart><canvas id=months></canvas></div></div>");
		sb.Append($"<div class=card><h2>Where it went</h2><div class=chart><canvas id=cats></canvas></div></div></div>");

		sb.Append("<div class='grid c2e'>");
		sb.Append($"<div class=card><h2>Fixed payments <a class=muted href='/fixed?m={month}'>see all →</a></h2><table>");
		foreach (var (f, st) in fixedRows.OrderBy(x => x.st.Status is FixedStatus.Paid or FixedStatus.PaidChanged).ThenBy(x => x.f.UsualDay).Take(6))
			sb.Append($"<tr><td>{Html(f.Def.Name)}</td><td class=num>{Money(st.Paid > 0 ? st.Paid : f.Def.Amount)}</td><td style='width:1%'>{StatusPill(st, f)}</td></tr>");
		if (fixedRows.Count == 0) sb.Append($"<tr><td class=muted>No confirmed fixed payments yet{(a.Suggestions.Count > 0 ? $" — <a href='/fixed?m={month}'>{a.Suggestions.Count} suggestions waiting</a>" : "")}.</td></tr>");
		sb.Append($"</table><div class=muted style='margin-top:8px'>{fixedRows.Count(x => x.st.Paid > 0)} of {fixedRows.Count} paid this month</div></div>");

		sb.Append($"<div class=card><h2>Biggest expenses <a class=muted href='/transactions?m={month}'>all transactions →</a></h2><table>");
		foreach (var i in a.Items.Where(i => i.Month == month && i.IsSpending).OrderBy(i => i.T.Amount).Take(6))
			sb.Append($"<tr><td class=muted style='width:1%'>{i.T.Date:dd/MM}</td><td>{Html(i.Name)}<span class=tag>{Dot(i.Category)}{Html(i.Category.Name)}</span></td><td class=num>{Money(-i.T.Amount)}</td></tr>");
		sb.Append("</table></div></div>");

		var months = a.Months.TakeLast(12).ToList();
		var cats = cur.ByCategory.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => (c: a.CategoryById(kv.Key), v: kv.Value)).ToList();
		var data = Js(new
		{
			l = months.Select(m => MonthName(m.Month)[..3] + " " + m.Month[2..4]),
			f = months.Select(m => m.Fixed), v = months.Select(m => m.Variable), s = months.Select(m => m.Saved), i = months.Select(m => m.Income),
			cl = cats.Select(c => c.c.Name), ci = cats.Select(c => c.c.Id.Value), cc = cats.Select(c => c.c.Color), cv = cats.Select(c => c.v)
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
	#endregion

	#region Private Helpers
	/// <summary>
	/// Appends one key-figure card.
	/// </summary>
	private static void Kpi(StringBuilder sb, string cls, string label, string value, string sub) =>
		sb.Append($"<div class='card kpi {cls}'><div class=muted>{Html(label)}</div><div class=v>{value}</div><div class=s>{Html(sub)}</div></div>");
	#endregion
}
