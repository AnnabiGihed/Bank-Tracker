using System.Globalization;

namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One row of the split editor: a label, a category and the amount as the owner types it. The amount is
///              read as a number of cents, with a comma or a dot, so the check that the parts add up is exact.
/// </summary>
public sealed class SplitPartEdit
{
	#region Properties
	/// <summary>Gets or sets what the part is; empty for none.</summary>
	public string Label { get; set; } = string.Empty;

	/// <summary>Gets or sets the part's category id; empty while none is chosen.</summary>
	public string CategoryId { get; set; } = string.Empty;

	/// <summary>Gets or sets the amount as typed; a comma or a dot separates the decimals.</summary>
	public string AmountText { get; set; } = string.Empty;

	/// <summary>Gets the amount in cents, rounded to the cent; zero when the text is empty or not a number.</summary>
	public long Cents
	{
		get
		{
			var text = AmountText.Replace(',', '.');
			return decimal.TryParse(text, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
				CultureInfo.InvariantCulture, out var value) ? (long)Math.Round(value * 100, MidpointRounding.AwayFromZero) : 0;
		}
	}
	#endregion
}
