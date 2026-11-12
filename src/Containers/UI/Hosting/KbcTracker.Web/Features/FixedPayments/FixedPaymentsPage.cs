using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Fixed payments tab: the suggestions to accept or dismiss, then the accepted fixed payments
///              with their status in the month.
/// </summary>
public static class FixedPaymentsPage
{
	#region Public Methods
	/// <summary>
	/// Renders the Fixed payments tab: the suggestions to accept or dismiss, then the accepted fixed payments with
	/// their status in the month.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? notice)
	{
		var today = DateOnly.FromDateTime(DateTime.Today);
		var sb = new StringBuilder();

		if (a.Suggestions.Count > 0)
		{
			AppendSuggestions(sb, a);
		}

		var rows = a.Fixed.Select(f => (f, State: f.StatusIn(month, today))).Where(x => x.State.Status != FixedStatus.NotStarted).ToList();
		var monthly = rows.Where(x => x.State.Status != FixedStatus.NotThisMonth || x.State.Paid > 0).ToList();
		sb.Append("<div class=card style='margin-bottom:12px'><div class=stat>");
		sb.Append(CultureInfo.InvariantCulture, $"<div><span class=muted>Expected in {MonthName(month)}</span><b>{Money(monthly.Sum(x => x.State.Paid > 0 ? x.State.Paid : x.f.Def.Amount))}</b></div>");
		sb.Append(CultureInfo.InvariantCulture, $"<div><span class=muted>Paid</span><b style='color:var(--inc)'>{Money(monthly.Sum(x => x.State.Paid))}</b></div>");
		sb.Append(CultureInfo.InvariantCulture, $"<div><span class=muted>Still to come</span><b style='color:var(--warn)'>{Money(monthly.Where(x => x.State.Status is FixedStatus.Due or FixedStatus.Late).Sum(x => x.f.Def.Amount))}</b></div>");
		sb.Append(CultureInfo.CurrentCulture, $"<div><span class=muted>Fixed payments</span><b>{a.Fixed.Count}</b></div></div></div>");

		sb.Append("<div class=card><div class=scroll><table><tr><th class=num>Day</th><th>Payment</th><th>Category</th><th class=num>Amount</th><th>This month</th><th></th></tr>");
		foreach (var (f, st) in rows.OrderBy(x => x.f.UsualDay))
		{
			var edit = new { Id = f.Def.Id.Value, f.Def.Name, f.Def.Amount, Cadence = f.Def.CadenceDays, CategoryId = f.Category.Id.Value };
			sb.Append(CultureInfo.CurrentCulture, $"""
				<tr><td class=num>{f.UsualDay}</td>
				<td><b>{Html(f.Def.Name)}</b><div class=muted>{Analysis.CadenceName(f.Def.CadenceDays)} · {f.Payments.Count} payment(s) · last {f.LastPaid:dd/MM/yy}</div></td>
				<td><span class=tag style='margin:0'>{Dot(f.Category)}{Html(f.Category.Name)}</span></td><td class=num>{Money(f.Def.Amount)}</td><td>{StatusPill(st, f)}</td>
				<td class=num><button class=icon title='Edit' onclick="editFixed({JsonAttribute(edit)})">✎</button><button class=icon title='Stop tracking' onclick="if(confirm('Stop tracking this fixed payment?'))api({JsonAttribute(new { action = "removeFixed", id = f.Def.Id.Value })})">✕</button></td></tr>
				""");
		}

		if (rows.Count == 0)
		{
			sb.Append("<tr><td colspan=6 class=empty>No confirmed fixed payments yet. Accept the suggestions above, or click ☆ next to a payment in Transactions.</td></tr>");
		}

		sb.Append("</table></div><div class=muted style='margin-top:10px'>Missing one? In <b>Transactions</b> click ☆ next to it.</div></div>");

		sb.Append(CultureInfo.InvariantCulture, $"""
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
		return Layout("fixed", month, a, s, sb.ToString(), script, notice);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Adds the card of suggested fixed payments, each with the payments it was found from and the buttons to accept
	/// or dismiss it.
	/// </summary>
	/// <param name="sb">The page being built.</param>
	/// <param name="a">The analysis that holds the suggestions.</param>
	private static void AppendSuggestions(StringBuilder sb, Analysis a)
	{
		sb.Append("<div class=card style='margin-bottom:12px'><h2>Possible fixed payments — please check <span class=muted style='text-transform:none'>accept to start tracking them</span></h2><div class='grid c3'>");
		foreach (var g in a.Suggestions)
		{
			var cat = a.CategoryById(g.CategoryId);
			sb.Append(CultureInfo.InvariantCulture, $"""
				<div class='card sug'>
				  <div style='display:flex;justify-content:space-between;gap:8px'><b>{Html(g.Name)}</b>{(g.HighConfidence ? "" : "<span class='pill nil' title='Only 2 payments seen so far'>maybe</span>")}</div>
				  <div class=amt>{Money(g.Amount)} <span class=muted>{g.Cadence.ToLowerInvariant()}</span></div>
				  <div class=muted>{Html(g.Reason)} · <span class=tag style='margin:0'>{Dot(cat)}{Html(cat.Name)}</span></div>
				  <div class=chips>{string.Concat(g.Items.TakeLast(6).Select(i => $"<span title='{Html(i.T.Description)}'>{i.T.Date:dd/MM} · {Money(-i.T.Amount)}</span>"))}</div>
				  <div style='display:flex;gap:6px'>
				    <button class='btn sm okb' onclick="api({JsonAttribute(new { action = "accept", key = g.Signature })})">✓ Fixed payment</button>
				    <button class='btn sm ghost' onclick="api({JsonAttribute(new { action = "dismiss", key = g.Signature })})">✕ Not fixed</button>
				  </div>
				</div>
				""");
		}

		sb.Append("</div></div>");
	}
	#endregion
}
