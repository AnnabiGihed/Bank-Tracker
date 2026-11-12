using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Transactions tab: the month's transactions with category pickers, the mark-as-fixed star
///              and the editor that splits a payment into parts with their own categories (ADR-0007).
/// </summary>
public static class TransactionsPage
{
	#region Constants
	/// <summary>
	/// The split editor: one row per part (label, category, amount), the amount left to assign and the buttons.
	/// </summary>
	private const string SplitDialog = """
		<dialog id=sd style='width:min(680px,94vw)'><form method=dialog onsubmit="saveSplit();return false">
		  <h3 style='margin:0'>Split <span id=sdName></span> · <span id=sdTotal></span></h3>
		  <div class=muted style='margin-top:4px'>Give each part its own category; the parts must add up to the payment. The first part takes what is left.</div>
		  <div id=sdRows></div>
		  <div style='display:flex;justify-content:space-between;align-items:center;margin-top:10px'>
		    <button type=button class='btn ghost sm' onclick="addPart({amount:0,categoryId:sp.cat,label:''});recalc()">+ Add part</button>
		    <b id=sdLeft></b>
		  </div>
		  <div style='display:flex;gap:8px;justify-content:space-between;margin-top:14px'>
		    <button type=button id=sdUnsplit class='btn ghost' style='color:var(--bad)' onclick="api({action:'unsplit',id:sp.id})">Remove split</button>
		    <span style='display:flex;gap:8px'><button type=button class='btn ghost' onclick=sd.close()>Cancel</button><button id=sdSave class=btn>Save</button></span>
		  </div>
		</form></dialog>
		""";

	/// <summary>
	/// The page script; <c>__OPTS__</c> is replaced by the options of the spending categories a part can use.
	/// </summary>
	private const string ScriptTemplate = """
		const q=document.getElementById('q'),type=document.getElementById('type'),cat=document.getElementById('cat'),unconf=document.getElementById('unconf');
		if(cat.value||q.value)type.value='all';
		function apply(){
		  let n=0,t=0;const s=q.value.trim().toLowerCase();
		  document.querySelectorAll('#tx tr[data-type]').forEach(r=>{
		    const ok=(type.value==='all'||r.dataset.type===type.value)&&(!cat.value||r.dataset.cats.split(' ').includes(cat.value))&&(!s||r.dataset.s.includes(s))&&(!unconf.checked||r.dataset.conf==='0');
		    r.style.display=ok?'':'none';
		    // a split payment counts only its parts in the filtered category
		    if(ok){n++;t+=cat.value&&r.dataset.p?(JSON.parse(r.dataset.p)[cat.value]||0):parseFloat(r.dataset.a)}});
		  document.getElementById('count').textContent=n+' transactions';
		  document.getElementById('total').textContent=(t>=0?'Total out: ':'Total in: ')+eur(Math.abs(t));
		}
		[q,type,cat,unconf].forEach(e=>e.addEventListener('input',apply));apply();
		// category change applies to the whole payee; the toast offers "only this one"
		async function setCat(x,sel){
		  const prev=await api({action:'payeeCategory',key:x.key,categoryId:sel.value},false);
		  sel.classList.remove('auto');
		  const label=sel.options[sel.selectedIndex].text.replace(/^(— )+/,'');
		  window._undo={x,prev,cat:sel.value};
		  toast(`<span>“${esc(label)}” set for all payments to <b>${esc(x.name)}</b></span><button onclick="onlyThis()">Only this one</button><button onclick="location.reload()">OK</button>`);
		}
		async function onlyThis(){const u=window._undo;await api({action:'txnOnly',key:u.x.key,id:u.x.id,categoryId:u.cat,restore:u.prev})}
		const spOpts=__OPTS__;let sp=null,spAuto=false;
		const cents=v=>Math.round((parseFloat(String(v).replace(',','.'))||0)*100);
		const amountOf=r=>r.querySelector('.spa');
		function openSplit(x){
		  sp=x;sdName.textContent=x.name;sdTotal.textContent=eur(x.amount);sdRows.replaceChildren();
		  const parts=x.parts??[{amount:x.amount,categoryId:x.cat,label:''},{amount:0,categoryId:x.cat,label:''}];
		  parts.forEach(addPart);spAuto=!x.parts;sdUnsplit.style.visibility=x.parts?'visible':'hidden';recalc();sd.showModal();
		}
		function addPart(p){
		  const r=document.createElement('div');
		  r.style.cssText='display:grid;grid-template-columns:1fr 1.3fr 100px 30px;gap:6px;margin-top:8px;align-items:center';
		  r.innerHTML=`<input class=spl maxlength=100 placeholder='Label (optional)' title='Label'><select class=spc title='Category'>${spOpts}</select><input class=spa inputmode=decimal title='Amount' style='text-align:right'><button type=button class=icon title='Remove this part'>✕</button>`;
		  r.querySelector('.spl').value=p.label??'';
		  if(p.categoryId)r.querySelector('.spc').value=p.categoryId;
		  amountOf(r).value=p.amount>0?Number(p.amount).toFixed(2):'';
		  amountOf(r).addEventListener('input',()=>{if(r===sdRows.firstElementChild)spAuto=false;recalc()});
		  r.querySelector('.spc').addEventListener('input',recalc);
		  r.querySelector('button').onclick=()=>{r.remove();recalc()};
		  sdRows.append(r);
		}
		function recalc(){
		  const rows=[...sdRows.children],total=Math.round(sp.amount*100);
		  if(spAuto&&rows.length){const rest=total-rows.slice(1).reduce((s,r)=>s+cents(amountOf(r).value),0);amountOf(rows[0]).value=rest>0?(rest/100).toFixed(2):''}
		  const left=total-rows.reduce((s,r)=>s+cents(amountOf(r).value),0);
		  sdLeft.textContent=left===0?'Everything is assigned':(left>0?'Left to assign: ':'Too much: ')+eur(Math.abs(left)/100);
		  sdLeft.style.color=left===0?'var(--inc)':'var(--bad)';
		  sdSave.disabled=!(left===0&&rows.length>=2&&rows.every(r=>cents(amountOf(r).value)>0&&r.querySelector('.spc').value));
		}
		function saveSplit(){
		  api({action:'split',id:sp.id,parts:[...sdRows.children].map(r=>({amount:cents(amountOf(r).value)/100,categoryId:r.querySelector('.spc').value,label:r.querySelector('.spl').value.trim()||null}))});
		}
		""";
	#endregion

	#region Public Methods
	/// <summary>
	/// Renders the Transactions tab: the month's transactions with category pickers, the mark-as-fixed star and the
	/// split editor, optionally filtered by category and search text. A split payment shows its parts instead of the
	/// category picker; filtering on a category matches any of its parts and totals only those parts.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <param name="cat">A category id to filter on, or <see langword="null"/> for all categories.</param>
	/// <param name="q">Text to search for, or <see langword="null"/> for no search.</param>
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? cat, string? q, string? notice)
	{
		var items = a.Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal)).ToList();
		var sb = new StringBuilder("<div class=filters>");
		sb.Append(CultureInfo.InvariantCulture, $"<input id=q placeholder='Search name, text or amount…' value='{Html(q)}' style='flex:1;min-width:200px'>");
		sb.Append("<select id=type><option value=spend>Spending</option><option value=income>Income</option><option value=save>Savings</option><option value=own>Own transfers</option><option value=all>Everything</option></select>");
		sb.Append("<select id=cat><option value=''>All categories</option>");
		AppendCategoryOptions(sb, a, items, cat);
		sb.Append("</select><label class=muted><input type=checkbox id=unconf> not confirmed only</label></div>");

		sb.Append("<div class=card><div class=scroll><table id=tx><tr><th>Date</th><th>Name</th><th>Category</th><th class=num>Amount</th><th title='Fixed payment'>Fixed</th></tr>");
		foreach (var i in items)
		{
			AppendRow(sb, a, month, i);
		}

		sb.Append("</table>");
		if (items.Count == 0)
		{
			sb.Append("<div class=empty>No transactions in this month.</div>");
		}

		sb.Append("</div><div class=muted style='margin-top:10px;display:flex;justify-content:space-between'><span id=count></span><b id=total></b></div></div>");
		sb.Append(SplitDialog);

		var script = ScriptTemplate.Replace("__OPTS__", Js(SpendingOptions(a)), StringComparison.Ordinal);
		return Layout("transactions", month, a, s, sb.ToString(), script, notice);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Appends one transaction row: its filter data, name, category picker or split parts, amount and fixed-payment star.
	/// </summary>
	private static void AppendRow(StringBuilder sb, Analysis a, string month, TxnInfo i)
	{
		var type = RowType(i);
		var search = $"{i.Name} {i.T.Description} {string.Join(' ', i.Parts.Select(p => p.Label))} {Math.Abs(i.T.Amount).ToString("0.00", Be)}".ToLowerInvariant();
		var ctx = JsonAttribute(new { key = i.SeriesKey, id = i.T.Id.Value, name = i.Name });
		var cats = string.Join(' ', i.Parts.SelectMany(p => CategoryAndAncestors(a, p.Category)).Distinct(StringComparer.Ordinal));
		var confirmed = i.CategoryConfirmed ? 1 : 0;
		sb.Append(CultureInfo.InvariantCulture, $"<tr data-type={type} data-cats='{Html(cats)}' data-conf={confirmed} data-s='{Html(search)}' data-a='{(-i.T.Amount).ToString(CultureInfo.InvariantCulture)}'");
		if (i.IsSplit)
		{
			sb.Append(CultureInfo.InvariantCulture, $" data-p='{JsonAttribute(RolledParts(a, i))}'");
		}

		sb.Append('>');
		sb.Append(CultureInfo.CurrentCulture, $"<td class=muted style='width:1%'>{i.T.Date:dd/MM}</td>");
		sb.Append(CultureInfo.InvariantCulture, $"<td><b>{Html(i.Name)}</b><div class='muted ell' title='{Html(i.T.Description)}'>{Html(i.T.Description)}</div></td>");
		sb.Append("<td>");
		if (i.IsSplit)
		{
			foreach (var p in i.Parts)
			{
				sb.Append(CultureInfo.InvariantCulture, $"<span class=tag style='margin:1px 4px 1px 0' title='{Html(string.Join(" › ", a.Tree.Path(p.Category.Id.Value).Select(c => c.Name)))}'>{Dot(p.Category)}{Html(p.Label ?? p.Category.Name)} {Money(Math.Abs(p.Amount))}</span>");
			}
		}
		else
		{
			sb.Append("<span style='white-space:nowrap'>").Append(CategorySelect(a, i.Category, i.CategoryConfirmed, $"setCat({ctx},this)"));
			if (!i.CategoryConfirmed)
			{
				sb.Append(CultureInfo.InvariantCulture, $"<button class=icon title='Confirm this category for {Html(i.Name)}' onclick=\"api({JsonAttribute(new { action = "confirm", key = i.SeriesKey })})\">✓</button>");
			}

			sb.Append("</span>");
		}

		if (i.EffectiveType == CategoryType.Expense)
		{
			sb.Append(CultureInfo.InvariantCulture, $"<button class=icon title='{(i.IsSplit ? "Edit the split" : "Split this payment into parts")}' onclick=\"openSplit({JsonAttribute(SplitContext(i))})\">✂</button>");
		}

		sb.Append("</td>");
		sb.Append(CultureInfo.InvariantCulture, $"<td class=num style='color:{(i.T.Amount > 0 ? "var(--inc)" : "inherit")}'>{Money(i.T.Amount)}</td>");
		sb.Append(FixedCell(i, month));
		sb.Append("</tr>");
	}

	/// <summary>
	/// Names the kind of a payment for the type filter: own transfer, saving, income or spending.
	/// </summary>
	/// <param name="i">The payment.</param>
	/// <returns>The filter value of the payment's kind.</returns>
	private static string RowType(TxnInfo i)
	{
		if (i.IsOwnMove)
		{
			return "own";
		}

		if (i.IsSaving)
		{
			return "save";
		}

		return i.T.Amount > 0 ? "income" : "spend";
	}

	/// <summary>
	/// Renders the cell that shows a payment's fixed-payment state: nothing for money in and own transfers, a star
	/// that opens the fixed payment when it is one, and otherwise a button that marks it as one.
	/// </summary>
	/// <param name="i">The payment.</param>
	/// <param name="month">The selected month, kept in the link to the fixed payments.</param>
	/// <returns>The cell's HTML.</returns>
	private static string FixedCell(TxnInfo i, string month)
	{
		if (i.T.Amount >= 0 || i.IsOwnMove)
		{
			return "<td></td>";
		}

		if (i.Recurring is not null)
		{
			return $"<td style='width:1%'><a class='icon on' title='Fixed payment: {Html(i.Recurring.Name)}' href='/fixed?m={month}' style='text-decoration:none'>★</a></td>";
		}

		return $"<td style='width:1%'><button class=icon title='Mark {Html(i.Name)} as a fixed payment' onclick=\"api({JsonAttribute(new { action = "mark", key = i.SeriesKey })})\">☆</button></td>";
	}

	/// <summary>
	/// Builds what the split editor starts from: the payment, its own category when parts can use it, and its current
	/// parts as positive amounts (<see langword="null"/> when it is not split yet). A "Difference" part that goes the
	/// other way (the payment became smaller than its parts) is left out, so the editor shows what is too much.
	/// </summary>
	private static object SplitContext(TxnInfo i) => new
	{
		id = i.T.Id.Value,
		name = i.Name,
		amount = Math.Abs(i.T.Amount),
		cat = i.Category.Type == CategoryType.Expense ? i.Category.Id.Value : "",
		parts = i.IsSplit
			? i.Parts.Where(p => Math.Sign(p.Amount) == Math.Sign(i.T.Amount))
				.Select(p => new { amount = Math.Abs(p.Amount), categoryId = p.Category.Id.Value, label = p.Label })
			: null,
	};

	/// <summary>
	/// Adds up a split payment's parts per category and every ancestor of it, as amounts out, so the category filter
	/// can total only the parts it matches.
	/// </summary>
	private static Dictionary<string, decimal> RolledParts(Analysis a, TxnInfo i)
	{
		var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
		foreach (var p in i.Parts)
		{
			foreach (var id in CategoryAndAncestors(a, p.Category))
			{
				totals[id] = totals.GetValueOrDefault(id) - p.Amount;
			}
		}

		return totals;
	}

	/// <summary>
	/// Lists a category's id followed by its ancestors' ids.
	/// </summary>
	private static IEnumerable<string> CategoryAndAncestors(Analysis a, Category c) =>
		a.Tree.Ancestors(c.Id.Value).Select(x => x.Id.Value).Prepend(c.Id.Value);

	/// <summary>
	/// Renders the options of the spending categories, the only ones a part can use, in tree order and indented by level.
	/// </summary>
	private static string SpendingOptions(Analysis a)
	{
		var sb = new StringBuilder();
		foreach (var (c, depth) in TreeOrder(a, ByName, c => c.Type == CategoryType.Expense))
			sb.Append(CultureInfo.InvariantCulture, $"<option value='{Html(c.Id.Value)}'>{Indent(depth)}{Html(c.Name)}</option>");
		return sb.ToString();
	}

	/// <summary>
	/// Appends the options of the category filter: the categories with payments (or parts of split payments) in the
	/// month and their ancestors, in tree order and indented by level, with the filtered one selected.
	/// </summary>
	private static void AppendCategoryOptions(StringBuilder sb, Analysis a, List<TxnInfo> items, string? cat)
	{
		var withPayments = items.SelectMany(i => i.Parts).SelectMany(p => CategoryAndAncestors(a, p.Category)).ToHashSet(StringComparer.Ordinal);
		foreach (var (c, depth) in TreeOrder(a, ByName, n => withPayments.Contains(n.Id.Value)))
		{
			var selected = string.Equals(c.Id.Value, cat, StringComparison.Ordinal) ? " selected" : "";
			sb.Append(CultureInfo.InvariantCulture, $"<option value='{Html(c.Id.Value)}'{selected}>{Indent(depth)}{Html(c.Name)}</option>");
		}
	}
	#endregion
}
