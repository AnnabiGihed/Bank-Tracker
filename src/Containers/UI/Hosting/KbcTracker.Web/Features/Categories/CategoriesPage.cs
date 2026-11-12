using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Categories tab: the category tree per type with its monthly totals rolled up through the
///              tree, and the dialog to create, move, edit and delete categories.
/// </summary>
public static class CategoriesPage
{
	#region Constants
	/// <summary>
	/// The dialog to create, move, edit and delete a category.
	/// </summary>
	private const string Dialog = """
		<dialog id=cd><form method=dialog onsubmit="saveCat();return false">
		  <h3 id=cdTitle style='margin:0'>Category</h3>
		  <label>Name</label><input id=cdName required>
		  <div id=cdParentRow><label>Parent category</label><select id=cdParent onchange=showType()></select></div>
		  <div id=cdTypeRow><label>Counts as</label><select id=cdType>
		    <option value=Expense>Spending</option><option value=Income>Income</option>
		    <option value=Savings>Savings (not spending)</option><option value=Transfer>Own transfer (ignored)</option></select></div>
		  <label>Colour</label><input id=cdColor type=color style='height:36px;padding:2px'>
		  <label>Keywords — payments whose shop/payee name contains one of these get this category automatically (comma separated)</label>
		  <textarea id=cdKw rows=3 placeholder='e.g. DELHAIZE, COLRUYT, ALDI'></textarea>
		  <div style='display:flex;gap:8px;justify-content:space-between;margin-top:14px'>
		    <button type=button id=cdDel class='btn ghost' style='color:var(--bad)' onclick=delCat()>Delete</button>
		    <span style='display:flex;gap:8px'><button type=button class='btn ghost' onclick=cd.close()>Cancel</button><button class=btn>Save</button></span>
		  </div>
		</form></dialog>
		""";

	/// <summary>
	/// The page script; <c>__T__</c> is replaced by the tree in picker order and <c>__MAX__</c> by the deepest level.
	/// </summary>
	private const string ScriptTemplate = """
		const tree=__T__,maxDepth=__MAX__,byId=Object.assign(Object.create(null),Object.fromEntries(tree.map(n=>[n.id,n])));
		let cdId=null,cdFixed=false,cdKind='';
		const expanded=new Set();
		function tog(id){expanded.has(id)?expanded.delete(id):expanded.add(id);refresh()}
		function refresh(){
		  const shown={};
		  document.querySelectorAll('table.cats tr[data-id]').forEach(r=>{
		    const p=r.dataset.parent,ok=!p||(shown[p]&&expanded.has(p));
		    shown[r.dataset.id]=ok;r.style.display=ok?'':'none';
		    const t=r.querySelector('button[data-toggle]');if(t)t.textContent=expanded.has(r.dataset.id)?'▾':'▸';
		  });
		}
		function height(id){return 1+Math.max(0,...tree.filter(n=>n.parent===id).map(n=>height(n.id)))}
		function below(id,ancestor){for(let p=id;p;p=byId[p]?.parent)if(p===ancestor)return true;return false}
		function fillParents(id,parent){
		  const h=id?height(id):1;
		  cdParent.replaceChildren(new Option('None (top level)',''));
		  tree.filter(n=>!(id&&below(n.id,id))&&n.depth+h<=maxDepth).forEach(n=>cdParent.add(new Option('— '.repeat(n.depth-1)+n.name,n.id)));
		  cdParent.value=parent||'';
		}
		function showType(){cdParentRow.style.display=cdFixed?'none':'';cdTypeRow.style.display=cdFixed||cdParent.value?'none':''}
		function editCat(c,parent){cdId=c?.Id??null;cdFixed=!!c?.BuiltIn;cdKind=c?.Type??'';cdTitle.textContent=c?'Edit category':parent?'New sub-category':'New category';cdName.value=c?.Name??'';cdType.value=c?.Type??'Expense';
		  fillParents(cdId,c?c.Parent:parent);showType();
		  cdColor.value=c?.Color??(parent?byId[parent].color:'#3454d1');cdKw.value=c?.Keywords??'';cdDel.style.visibility=c&&!c.BuiltIn?'visible':'hidden';cd.showModal()}
		function saveCat(){api({action:'saveCategory',id:cdId,name:cdName.value,type:cdFixed?cdKind:(cdParent.value?'':cdType.value),parentId:cdFixed?undefined:cdParent.value,color:cdColor.value,keywords:cdKw.value})}
		function delCat(){const p=byId[cdId]?.parent;
		  if(confirm('Delete this category? Its payments and rules move to '+(p?'its parent, “'+byId[p].name+'”.':'“Other”; its payee rules show up in To review again.')))api({action:'deleteCategory',id:cdId})}
		refresh();
		""";
	#endregion

	#region Public Methods
	/// <summary>
	/// Renders the Categories tab: the category tree to create, edit, move or delete categories in, with their monthly
	/// totals. A category's total and its comparison with the previous months include its sub-categories; the
	/// sub-categories are collapsed until the row's toggle is used.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session shown in the header, or <see langword="null"/> when not connected.</param>
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? notice)
	{
		var rolled = RolledTotals(a, a.Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal)));
		var previous = a.AvailableMonths.Where(m => string.CompareOrdinal(m, month) < 0).Take(3)
			.Select(m => RolledTotals(a, a.Items.Where(i => string.Equals(i.Month, m, StringComparison.Ordinal)))).ToList();
		var counts = a.Items.GroupBy(i => i.Category.Id.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (decimal)g.Count(), StringComparer.Ordinal);
		var payments = a.Tree.RollUp(counts);
		var ordered = TreeOrder(a, siblings => siblings.OrderByDescending(c => rolled.GetValueOrDefault(c.Id.Value)).ThenBy(c => c.Name, StringComparer.CurrentCulture));

		var sb = new StringBuilder();
		sb.Append(CultureInfo.InvariantCulture, $"<div class=filters><span class=muted style='margin-right:auto'>{MonthName(month)} · click a category to see its transactions</span><button class=btn onclick=\"editCat(null)\">+ New category</button></div>");
		foreach (var group in ordered.GroupBy(x => a.Tree.Root(x.Category.Id.Value)!.Type).OrderBy(g => g.Key))
		{
			AppendTypeCard(sb, a, month, group.Key, [.. group], new Totals(rolled, previous, payments));
		}

		sb.Append(Dialog);
		var picker = ordered.Select(x => new { id = x.Category.Id.Value, parent = a.Tree.Parent(x.Category.Id.Value)?.Id.Value ?? "", name = x.Category.Name, color = x.Category.Color, depth = x.Depth });
		var script = ScriptTemplate.Replace("__T__", Js(picker), StringComparison.Ordinal)
			.Replace("__MAX__", CategoryTree.MaxDepth.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
		return Layout("categories", month, a, s, sb.ToString(), script, notice);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Adds up what the given transactions amount to per category, including everything below it: spending and savings
	/// as positive amounts, income as positive income.
	/// </summary>
	private static IReadOnlyDictionary<string, decimal> RolledTotals(Analysis a, IEnumerable<TxnInfo> items) =>
		a.Tree.RollUp(items.GroupBy(i => i.Category.Id.Value, StringComparer.Ordinal).ToDictionary(
			g => g.Key,
			g => g.Sum(i => i.Category.Type == CategoryType.Income ? i.T.Amount : -i.T.Amount),
			StringComparer.Ordinal));

	/// <summary>
	/// Appends the card of one category type: its heading with the total of the top-level categories and the table of
	/// its categories in tree order.
	/// </summary>
	private static void AppendTypeCard(StringBuilder sb, Analysis a, string month, CategoryType type, List<(Category Category, int Depth)> rows, Totals totals)
	{
		var tops = rows.Where(x => x.Depth == 1).Select(x => totals.Month.GetValueOrDefault(x.Category.Id.Value)).ToList();
		var max = tops.DefaultIfEmpty(0).Max();
		var title = type switch { CategoryType.Expense => "Spending", CategoryType.Income => "Income", CategoryType.Savings => "Savings", _ => "Own transfers (not counted)" };
		sb.Append(CultureInfo.InvariantCulture, $"<div class=card style='margin-bottom:12px'><h2>{title}<span>{Money(tops.Sum())}</span></h2><table class=cats><tr><th>Category</th><th style='width:30%'></th><th class=num>{MonthName(month)}</th><th class=num>vs. previous months</th><th>Keywords</th><th></th></tr>");
		foreach (var (c, depth) in rows)
		{
			var id = c.Id.Value;
			decimal? average = totals.Previous.Count == 0 ? null : totals.Previous.Average(p => p.GetValueOrDefault(id));
			AppendRow(sb, a, month, new RowFigures(c, depth, totals.Month.GetValueOrDefault(id), average, totals.Payments.GetValueOrDefault(id), max, type));
		}

		sb.Append("</table></div>");
	}

	/// <summary>
	/// Appends one category row, indented by its level, with the toggle for its sub-categories and the buttons to add
	/// a sub-category (below the deepest level there is none) and to edit it. Sub-categories start hidden.
	/// </summary>
	private static void AppendRow(StringBuilder sb, Analysis a, string month, RowFigures row)
	{
		var c = row.Category;
		var parent = a.Tree.Parent(c.Id.Value)?.Id.Value ?? "";
		var edit = new { Id = c.Id.Value, c.Name, Type = c.Type.ToString(), c.Color, Keywords = string.Join(", ", c.Keywords), c.BuiltIn, Parent = parent };
		var toggle = a.Tree.Children(c.Id.Value).Count > 0
			? $"<button class=icon data-toggle title='Show or hide the sub-categories' onclick=\"event.stopPropagation();tog({JsonAttribute(c.Id.Value)})\" style='width:26px'>▸</button>"
			: "<span style='display:inline-block;width:26px'></span>";
		var indent = (row.Depth - 1) * 20;
		var hidden = row.Depth > 1 ? " style='display:none'" : "";
		var bar = row.Max <= 0 || row.Value <= 0 ? 0 : row.Value / row.Max * 100;
		var value = row.Value == 0 ? "<span class=muted>—</span>" : Money(row.Value);
		var inv = CultureInfo.InvariantCulture;
		sb.Append(inv, $"<tr data-id='{Html(c.Id.Value)}' data-parent='{Html(parent)}'{hidden}>");
		sb.Append(inv, $"<td style='cursor:pointer' onclick=\"location.href='/transactions?m={month}&cat={Uri.EscapeDataString(c.Id.Value)}'\"><b style='display:flex;align-items:center;gap:8px;padding-left:{indent}px'>{toggle}{Dot(c)}{Html(c.Name)}</b><div class=muted style='padding-left:{indent + 26}px'>{row.Payments:0} payment(s) in total</div></td>");
		sb.Append(inv, $"<td><div class=bar><i style='width:{bar:0}%;background:{Html(c.Color)}'></i></div></td>");
		sb.Append(inv, $"<td class=num>{value}</td><td class=num>{Comparison(row.Value, row.Average, row.Type)}</td>");
		sb.Append(inv, $"<td class='muted ell' style='max-width:220px'>{Html(string.Join(", ", c.Keywords))}</td>");
		sb.Append("<td class=num style='white-space:nowrap'>");
		if (row.Depth < CategoryTree.MaxDepth)
		{
			sb.Append(inv, $"<button class='btn sm ghost' title='Add a sub-category to {Html(c.Name)}' onclick=\"editCat(null,{JsonAttribute(c.Id.Value)})\">+ sub-category</button> ");
		}

		sb.Append(inv, $"<button class=icon title='Edit' onclick=\"editCat({JsonAttribute(edit)})\">✎</button></td></tr>");
	}

	/// <summary>
	/// Renders how a month's spending compares with the average of the previous months: a red or green pill when it
	/// differs by more than 15 percent, "similar" otherwise, and a dash when there is nothing to compare or the
	/// category is not spending.
	/// </summary>
	private static string Comparison(decimal value, decimal? average, CategoryType type)
	{
		if (average is null || average == 0 || type != CategoryType.Expense)
		{
			return "<span class=muted>—</span>";
		}

		if (value > average * 1.15m)
		{
			return $"<span class='pill bad'>+{WholeMoney(value - average.Value)}</span>";
		}

		return value < average * 0.85m ? $"<span class='pill ok'>−{WholeMoney(average.Value - value)}</span>" : "<span class='pill nil'>similar</span>";
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The rolled-up figures of the tab: the month's total, the totals of the previous months and the
	///              number of payments, each per category id and including the sub-categories.
	/// </summary>
	/// <param name="Month">The selected month's total per category.</param>
	/// <param name="Previous">The total per category of each of the previous months.</param>
	/// <param name="Payments">The number of payments ever made per category.</param>
	private sealed record Totals(IReadOnlyDictionary<string, decimal> Month, List<IReadOnlyDictionary<string, decimal>> Previous, IReadOnlyDictionary<string, decimal> Payments);

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : What one category row shows.
	/// </summary>
	/// <param name="Category">The category.</param>
	/// <param name="Depth">Its level, one for a top-level category.</param>
	/// <param name="Value">Its total in the month, sub-categories included.</param>
	/// <param name="Average">The average of its totals in the previous months, or <see langword="null"/> without any.</param>
	/// <param name="Payments">The number of payments ever made to it, sub-categories included.</param>
	/// <param name="Max">The largest top-level total of its type, the full width of the bar.</param>
	/// <param name="Type">How its type counts in the totals.</param>
	private sealed record RowFigures(Category Category, int Depth, decimal Value, decimal? Average, decimal Payments, decimal Max, CategoryType Type);
	#endregion
}
