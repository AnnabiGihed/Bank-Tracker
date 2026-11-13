namespace KbcTracker.Components.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The chart of the last months: fixed, variable and saved money as stacked columns and the income as a
///              line, drawn by Radzen from CSS colour variables. A table with the same numbers, hidden visually but
///              readable by a screen reader, stands next to it, because a chart alone is not accessible.
/// </summary>
public partial class MonthChart : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the months to draw, oldest first.</summary>
	[Parameter]
	public IReadOnlyList<MonthPoint> Points { get; set; } = [];
	#endregion

	#region Private Helpers
	/// <summary>Formats a value on the amount axis.</summary>
	private static string FormatAxis(object value) => Formats.Belgian.WholeMoney(Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));

	/// <summary>Formats an amount for the table.</summary>
	private static string Format(decimal amount) => Formats.Belgian.Money(amount);
	#endregion
}
