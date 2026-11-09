using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static KbcTracker.Web.Features.Shared.Pages.PageKit;

namespace KbcTracker.Web.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders the bank picker shown before connecting to a bank.
/// </summary>
public static class BankPickerPage
{
	#region Public Methods
	/// <summary>
	/// Renders the bank picker shown before connecting, with KBC banks first.
	/// </summary>
	/// <param name="banks">The banks offered by Enable Banking.</param>
	/// <returns>The complete HTML page.</returns>
	public static string Render(List<(string Name, string Country)> banks)
	{
		var sb = new StringBuilder($"<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>Choose bank</title><style>{Css}</style></head><body><main style='max-width:640px'><h1>Choose your bank</h1><div class=card><table>");
		foreach (var b in banks.OrderByDescending(b => b.Name.Contains("KBC", StringComparison.OrdinalIgnoreCase)).ThenBy(b => b.Name))
			sb.Append($"<tr><td>{Html(b.Name)}</td><td class=num><a class=btn href='/connect?name={Uri.EscapeDataString(b.Name)}&country={b.Country}'>Connect</a></td></tr>");
		sb.Append("</table></div><p><a href=/>← back</a></p></main></body></html>");
		return sb.ToString();
	}
	#endregion
}
