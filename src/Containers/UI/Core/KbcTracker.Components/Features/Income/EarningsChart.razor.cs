namespace KbcTracker.Components.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The small chart of the earnings of the last months, drawn by Radzen as columns from a CSS colour
///              variable. A table with the same numbers, hidden visually but readable by a screen reader, stands next to
///              it, because a chart alone is not accessible.
/// </summary>
public partial class EarningsChart : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the months to draw, oldest first.</summary>
	[Parameter]
	public IReadOnlyList<EarningsPoint> Points { get; set; } = [];
	#endregion

	#region Private Helpers
	/// <summary>Formats a value on the amount axis.</summary>
	private static string FormatAxis(object value) => Formats.Belgian.WholeMoney(Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));

	/// <summary>Formats an amount for the table.</summary>
	private static string Format(decimal amount) => Formats.Belgian.Money(amount);
	#endregion
}
