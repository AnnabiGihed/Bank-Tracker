using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the To review tab: the payees whose automatic category is not confirmed yet.
/// </summary>
public static class ReviewPage
{
	#region Public Methods
	/// <summary>
	/// Renders the To review tab: the payees whose automatic category the user has not confirmed yet.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? notice)
	{
		var sb = new StringBuilder();
		if (a.Review.Count == 0)
			return Layout("review", month, a, s, Empty("All categories are confirmed. 🎉 New payees will show up here after a sync."), notice: notice);

		sb.Append($"<div class=filters><span class=muted style='margin-right:auto'>These payees got a category automatically. Confirm it (✓) or pick another — it then applies to all their payments, past and future.</span>");
		sb.Append("<select id=dir><option value=all>All</option><option value=out>Money out</option><option value=in>Money in</option></select>");
		sb.Append("<button class='btn okb' onclick=confirmVisible()>✓ Confirm all shown</button></div>");
		sb.Append("<div class=card><div class=scroll><table id=rv><tr><th>Payee</th><th class=num>Payments</th><th class=num>Total</th><th class=num>Last</th><th>Category</th><th></th></tr>");
		foreach (var r in a.Review)
		{
			var ctx = JsonAttribute(new { key = r.SeriesKey });
			sb.Append($"<tr data-dir={(r.Incoming ? "in" : "out")} data-key='{Html(r.SeriesKey)}'>");
			sb.Append($"<td><b>{Html(r.Name)}</b><div class='muted ell' title='{Html(r.Sample)}'>{Html(r.Sample)}</div></td>");
			sb.Append($"<td class=num>{r.Count}</td><td class=num style='color:{(r.Total > 0 ? "var(--inc)" : "inherit")}'>{Money(r.Total)}</td><td class='num muted'>{r.Last:dd/MM/yy}</td>");
			sb.Append("<td>").Append(CategorySelect(a, r.Category, false, $"api({{action:'payeeCategory',key:{ctx}.key,categoryId:this.value}})")).Append("</td>");
			sb.Append($"<td class=num><button class='btn sm okb' onclick=\"api({JsonAttribute(new { action = "confirm", key = r.SeriesKey })})\">✓</button></td></tr>");
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
		return Layout("review", month, a, s, sb.ToString(), script, notice);
	}
	#endregion
}
