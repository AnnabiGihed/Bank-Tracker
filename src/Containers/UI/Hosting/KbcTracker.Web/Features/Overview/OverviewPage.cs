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
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? notice)
	{
		if (a.Items.Count == 0)
			return Layout("overview", month, a, s, Empty(s is null
				? "Connect your bank to get started: click <b>Connect bank</b> and log in with itsme."
				: "No transactions yet — click <b>↻ Sync</b>."), notice: notice);

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
		sb.Append($"<div class=card><h2>Where it went</h2><div id=crumb class=muted style='margin:-4px 0 6px'></div><div class=chart><canvas id=cats></canvas></div></div></div>");

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

		var script = ChartScript(a, cur, month);
		return Layout("overview", month, a, s, sb.ToString(), script, notice);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Builds the script of the two charts: the last months as stacked bars, and the doughnut of where the month's
	/// money went, which starts at the top-level categories and drills down to a category's sub-categories.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="cur">The selected month's figures.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <returns>The JavaScript that draws the charts.</returns>
	private static string ChartScript(Analysis a, MonthSummary cur, string month)
	{
		var months = a.Months.TakeLast(12).ToList();
		var spending = a.CategoryList.Where(c => a.Tree.Root(c.Id.Value)?.Type == CategoryType.Expense).ToList();
		var spendingIds = spending.Select(c => c.Id.Value).ToHashSet(StringComparer.Ordinal);
		var data = Js(new
		{
			l = months.Select(m => MonthName(m.Month)[..3] + " " + m.Month[2..4]),
			f = months.Select(m => m.Fixed), v = months.Select(m => m.Variable), s = months.Select(m => m.Saved), i = months.Select(m => m.Income),
			t = spending.Select(c => new { id = c.Id.Value, parent = a.Tree.Parent(c.Id.Value)?.Id.Value, name = c.Name, color = c.Color }),
			o = cur.ByCategory.Where(kv => kv.Value > 0 && spendingIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)
		});
		var template = """
			const d=__D__;
			new Chart(document.getElementById('months'),{type:'bar',data:{labels:d.l,datasets:[
			  {label:'Fixed',data:d.f,backgroundColor:css('--fixed'),stack:'s',borderRadius:4},
			  {label:'Variable',data:d.v,backgroundColor:css('--var'),stack:'s',borderRadius:4},
			  {label:'Saved',data:d.s,backgroundColor:css('--save'),stack:'s',borderRadius:4},
			  {label:'Income',data:d.i,type:'line',borderColor:css('--inc'),backgroundColor:css('--inc'),tension:.3,pointRadius:3}]},
			  options:{maintainAspectRatio:false,interaction:{mode:'index',intersect:false},plugins:{legend:{position:'bottom'},tooltip:{callbacks:{label:c=>`${c.dataset.label}: ${eur(c.raw)}`}}},scales:{y:{ticks:{callback:v=>v.toLocaleString('fr-BE')+' €'}}}}});
			const nul=()=>Object.create(null),byId=nul(),kids=nul(),tot=nul(),own=Object.assign(nul(),d.o);let at='';
			d.t.forEach(n=>{byId[n.id]=n;(kids[n.parent??''] ??= []).push(n.id)});
			const roll=id=>tot[id]=(own[id]||0)+(kids[id]||[]).reduce((s,c)=>s+roll(c),0);
			(kids['']||[]).forEach(roll);
			const goTo=id=>location.href='/transactions?m=__M__&cat='+encodeURIComponent(id);
			const drills=id=>(kids[id]||[]).some(c=>tot[c]>0);
			const cents=v=>Math.round(v*100)/100;
			const palette=['#3454d1','#f2994a','#27ae60','#9b51e0','#eb5757','#2d9cdb','#f2c94c','#e84393','#00cec9','#6c5ce7','#e17055','#16a085'];
			function slices(){
			  const r=(kids[at]||[]).filter(c=>tot[c]>0).map(c=>({id:c,name:byId[c].name,color:byId[c].color,v:cents(tot[c]),drill:drills(c)})).sort((x,y)=>y.v-x.v);
			  // Sub-categories inherit their top-level colour, so one level down every slice would look the same.
			  if(at)r.forEach((s,i)=>s.color=palette[i%palette.length]);
			  if(at&&own[at]>0)r.push({id:at,name:byId[at].name+' – not specified',color:'#a0a7b4',v:cents(own[at]),drill:false});
			  return r;
			}
			let shown=[];
			const doughnut=new Chart(document.getElementById('cats'),{type:'doughnut',data:{labels:[],datasets:[{data:[],backgroundColor:[],borderWidth:0}]},
			  options:{maintainAspectRatio:false,cutout:'62%',plugins:{legend:{position:'right',labels:{boxWidth:10}},tooltip:{callbacks:{label:c=>`${c.label}: ${eur(c.raw)}`}}},
			  onClick:(e,el)=>{if(!el.length)return;const s=shown[el[0].index];if(s.drill){at=s.id;draw()}else goTo(s.id)}}});
			function crumbs(){
			  const box=document.getElementById('crumb');box.replaceChildren();
			  const path=[];for(let p=at;p;p=byId[p]?.parent)path.unshift(p);
			  [''].concat(path).forEach((id,i,all)=>{
			    if(i)box.append(' › ');
			    const last=i===all.length-1,label=id?byId[id].name:'All';
			    if(last){const b=document.createElement('b');b.textContent=label;box.append(b)}
			    else{const l=document.createElement('a');l.href='#';l.textContent=label;l.onclick=e=>{e.preventDefault();at=id;draw()};box.append(l)}
			  });
			}
			function draw(){
			  shown=slices();
			  doughnut.data.labels=shown.map(s=>s.name);
			  doughnut.data.datasets[0].data=shown.map(s=>s.v);
			  doughnut.data.datasets[0].backgroundColor=shown.map(s=>s.color);
			  doughnut.update();crumbs();
			}
			draw();
			""";
		return template.Replace("__D__", data, StringComparison.Ordinal).Replace("__M__", month, StringComparison.Ordinal);
	}

	/// <summary>
	/// Appends one key-figure card.
	/// </summary>
	private static void Kpi(StringBuilder sb, string cls, string label, string value, string sub) =>
		sb.Append($"<div class='card kpi {cls}'><div class=muted>{Html(label)}</div><div class=v>{value}</div><div class=s>{Html(sub)}</div></div>");
	#endregion
}
