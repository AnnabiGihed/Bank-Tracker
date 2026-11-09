using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Categories tab: the category definitions and their monthly totals.
/// </summary>
public static class CategoriesPage
{
	#region Public Methods
	/// <summary>
	/// Renders the Categories tab: the category definitions to create, edit or delete, with their monthly totals.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s)
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
			sb.Append($"<div class=card style='margin-bottom:12px'><h2>{title}<span>{Money(rows.Sum(r => r.v))}</span></h2><table><tr><th>Category</th><th style='width:30%'></th><th class=num>{MonthName(month)}</th><th class=num>vs. previous months</th><th>Keywords</th><th></th></tr>");
			foreach (var (c, v, avg, n) in rows)
			{
				var diff = avg is null || avg == 0 || group.Key != CategoryType.Expense ? "<span class=muted>—</span>"
					: v > avg * 1.15m ? $"<span class='pill bad'>+{WholeMoney(v - avg.Value)}</span>"
					: v < avg * 0.85m ? $"<span class='pill ok'>−{WholeMoney(avg.Value - v)}</span>"
					: "<span class='pill nil'>similar</span>";
				var edit = new { c.Id, c.Name, Type = c.Type.ToString(), c.Color, Keywords = string.Join(", ", c.Keywords), c.BuiltIn };
				sb.Append($"<tr><td style='cursor:pointer' onclick=\"location.href='/transactions?m={month}&cat={Uri.EscapeDataString(c.Id)}'\"><b style='display:flex;align-items:center;gap:8px'>{Dot(c)}{Html(c.Name)}</b><div class=muted>{n} payment(s) in total</div></td>");
				sb.Append($"<td><div class=bar><i style='width:{(max <= 0 || v <= 0 ? 0 : v / max * 100):0}%;background:{Html(c.Color)}'></i></div></td>");
				sb.Append($"<td class=num>{(v == 0 ? "<span class=muted>—</span>" : Money(v))}</td><td class=num>{diff}</td>");
				sb.Append($"<td class='muted ell' style='max-width:220px'>{Html(string.Join(", ", c.Keywords))}</td>");
				sb.Append($"<td class=num><button class=icon title='Edit' onclick=\"editCat({JsonAttribute(edit)})\">✎</button></td></tr>");
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
	#endregion
}
