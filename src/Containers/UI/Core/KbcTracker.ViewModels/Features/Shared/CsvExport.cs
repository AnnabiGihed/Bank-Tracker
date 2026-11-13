using System.Globalization;
using System.Text;

namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exports the backend's classified workspace without reading local history files.
/// </summary>
public static class CsvExport
{
	#region Public Methods
	/// <summary>Writes permanent history as semicolon-separated values, oldest first; a split payment is written as one
	/// line per part, with the part's amount, category and label, so sums per category stay right.</summary>
	/// <param name="analysis">The workspace received from the backend.</param>
	/// <returns>The CSV document.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> is <see langword="null"/>.</exception>
	public static string Write(Analysis analysis)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		var csv = new StringBuilder("Date;Amount;Currency;Name;Category;Confirmed;Fixed payment;Description;Part\n");
		foreach (var item in analysis.Items.OrderBy(i => i.T.Date))
		{
			foreach (var part in item.Parts)
			{
				csv.Append(CultureInfo.InvariantCulture,
					$"{item.T.Date:yyyy-MM-dd};{part.Amount};{item.T.Currency};{Quote(item.Name)};{Quote(part.Category.Name)};{(item.CategoryConfirmed ? "yes" : "")};{Quote(item.Recurring?.Name ?? "")};{Quote(item.T.Description)};{Quote(part.Label ?? "")}\n");
			}
		}

		return csv.ToString();
	}
	#endregion

	#region Private Helpers
	/// <summary>Escapes embedded quotes in a spreadsheet field.</summary>
	private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
	#endregion
}
