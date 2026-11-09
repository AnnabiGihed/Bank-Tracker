using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Transactions tab: the month's transactions with category pickers and the mark-as-fixed star.
/// </summary>
public static class TransactionsPage
{
	#region Public Methods
	/// <summary>
	/// Renders the Transactions tab: the month's transactions with category pickers and the mark-as-fixed star,
	/// optionally filtered by category and search text.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <param name="cat">A category id to filter on, or <see langword="null"/> for all categories.</param>
	/// <param name="q">Text to search for, or <see langword="null"/> for no search.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? cat, string? q)
	{
		var items = a.Items.Where(i => i.Month == month).ToList();
		var sb = new StringBuilder("<div class=filters>");
		sb.Append($"<input id=q placeholder='Search name, text or amount…' value='{Html(q)}' style='flex:1;min-width:200px'>");
		sb.Append("<select id=type><option value=spend>Spending</option><option value=income>Income</option><option value=save>Savings</option><option value=own>Own transfers</option><option value=all>Everything</option></select>");
		sb.Append("<select id=cat><option value=''>All categories</option>");
		foreach (var c in items.Select(i => i.Category).DistinctBy(c => c.Id).OrderBy(c => c.Name))
			sb.Append($"<option value='{Html(c.Id)}'{(c.Id == cat ? " selected" : "")}>{Html(c.Name)}</option>");
		sb.Append("</select><label class=muted><input type=checkbox id=unconf> not confirmed only</label></div>");

		sb.Append("<div class=card><div class=scroll><table id=tx><tr><th>Date</th><th>Name</th><th>Category</th><th class=num>Amount</th><th title='Fixed payment'>Fixed</th></tr>");
		foreach (var i in items)
		{
			var type = i.IsOwnMove ? "own" : i.IsSaving ? "save" : i.T.Amount > 0 ? "income" : "spend";
			var search = $"{i.Name} {i.T.Description} {Math.Abs(i.T.Amount).ToString("0.00", Be)}".ToLowerInvariant();
			var ctx = JsonAttribute(new { key = i.SeriesKey, id = i.T.Id, name = i.Name });
			sb.Append($"<tr data-type={type} data-cat='{Html(i.Category.Id)}' data-conf={(i.CategoryConfirmed ? 1 : 0)} data-s='{Html(search)}' data-a='{(-i.T.Amount).ToString(CultureInfo.InvariantCulture)}'>");
			sb.Append($"<td class=muted style='width:1%'>{i.T.Date:dd/MM}</td>");
			sb.Append($"<td><b>{Html(i.Name)}</b><div class='muted ell' title='{Html(i.T.Description)}'>{Html(i.T.Description)}</div></td>");
			sb.Append("<td style='white-space:nowrap'>").Append(CategorySelect(a, i.Category, i.CategoryConfirmed, $"setCat({ctx},this)"));
			if (!i.CategoryConfirmed) sb.Append($"<button class=icon title='Confirm this category for {Html(i.Name)}' onclick=\"api({JsonAttribute(new { action = "confirm", key = i.SeriesKey })})\">✓</button>");
			sb.Append("</td>");
			sb.Append($"<td class=num style='color:{(i.T.Amount > 0 ? "var(--inc)" : "inherit")}'>{Money(i.T.Amount)}</td>");
			sb.Append(i.T.Amount >= 0 || i.IsOwnMove ? "<td></td>"
				: i.Recurring is not null
					? $"<td style='width:1%'><a class='icon on' title='Fixed payment: {Html(i.Recurring.Name)}' href='/fixed?m={month}' style='text-decoration:none'>★</a></td>"
					: $"<td style='width:1%'><button class=icon title='Mark {Html(i.Name)} as a fixed payment' onclick=\"api({JsonAttribute(new { action = "mark", key = i.SeriesKey })})\">☆</button></td>");
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
	#endregion
}
