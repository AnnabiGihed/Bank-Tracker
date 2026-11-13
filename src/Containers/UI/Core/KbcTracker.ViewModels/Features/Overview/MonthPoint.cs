namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One month of the chart of the last months: income as a line, and fixed spending, variable spending and
///              savings as stacked bars.
/// </summary>
/// <param name="Month">The month, as <c>yyyy-MM</c>.</param>
/// <param name="Label">The axis label, such as <c>Mar 26</c>.</param>
/// <param name="Income">The month's income.</param>
/// <param name="Fixed">The month's fixed payments.</param>
/// <param name="Variable">The month's other spending.</param>
/// <param name="Saved">The month's savings and investments.</param>
public sealed record MonthPoint(string Month, string Label, decimal Income, decimal Fixed, decimal Variable, decimal Saved);
