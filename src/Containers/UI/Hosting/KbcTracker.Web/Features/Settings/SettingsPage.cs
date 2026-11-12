using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.Settings;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the Settings tab: the bank connection, the stored history, the data folder and the CSV export.
/// </summary>
public static class SettingsPage
{
	#region Public Methods
	/// <summary>
	/// Renders the Settings tab: the bank connection, the stored history, the data folder and the CSV export.
	/// </summary>
	/// <param name="a">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="s">The bank session, or <see langword="null"/> when not connected.</param>
	/// <param name="notice">A one-time failure message to show above the page, or <see langword="null"/> for none.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(Analysis a, string month, SessionInfo? s, string? notice)
	{
		var sb = new StringBuilder("<div class='grid c2e'>");
		sb.Append("<div class=card><h2>Bank connection</h2>");
		if (s is null)
		{
			sb.Append("<p>Not connected.</p><a class=btn href=/banks>Connect KBC Brussels</a>");
		}
		else
		{
			sb.Append(CultureInfo.CurrentCulture, $"""
				<table>
				<tr><td class=muted>Accounts</td><td>{s.AccountUids.Count}</td></tr>
				<tr><td class=muted>Bank access valid until</td><td>{s.ValidUntil.ToLocalTime():dd/MM/yyyy} <span class=muted>(then click Reconnect)</span></td></tr>
				<tr><td class=muted>Last sync</td><td>{s.LastSync?.ToLocalTime():dd/MM/yyyy HH:mm}{(s.LastError is null ? "" : $" <span class='pill bad'>failed</span>")}</td></tr>
				</table>
				<div style='display:flex;gap:8px;flex-wrap:wrap;margin-top:12px'>
				  <form method=post action='/sync?back=/settings'><button class=btn>↻ Sync now</button></form>
				  <a class='btn ghost' href=/banks>Reconnect / add account</a>
				</div>
				""");
		}

		sb.Append("</div>");

		sb.Append("<div class=card><h2>Your history</h2><table>");
		sb.Append(CultureInfo.CurrentCulture, $"<tr><td class=muted>Stored</td><td>{(a.Items.Count == 0 ? "—" : $"{a.Items.Min(i => i.T.Date):dd/MM/yyyy} → {a.Items.Max(i => i.T.Date):dd/MM/yyyy}")}</td></tr>");
		sb.Append(CultureInfo.CurrentCulture, $"<tr><td class=muted>Transactions</td><td>{a.Items.Count}</td></tr>");
		sb.Append(CultureInfo.CurrentCulture, $"<tr><td class=muted>Fixed payments</td><td>{a.Fixed.Count}</td></tr>");
		sb.Append("<tr><td class=muted>Storage</td><td>Central PostgreSQL backend</td></tr></table>");
		sb.Append("""
			<p class=muted>KBC only shares the last ~90 days. Your backend keeps every downloaded transaction in PostgreSQL.
			Keep the backend running for automatic sync and back up its database regularly. The web app does not store or sync bank data locally.</p>
			<a class='btn ghost' href=/export.csv>Export CSV</a>
			""");
		sb.Append("</div></div>");
		return Layout("settings", month, a, s, sb.ToString(), notice: notice);
	}
	#endregion
}
