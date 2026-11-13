namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One month of the earnings chart of the Income tab.
/// </summary>
/// <param name="Month">The month, as <c>yyyy-MM</c>.</param>
/// <param name="Label">The axis label, such as <c>Mar 26</c>.</param>
/// <param name="Earnings">The month's earnings, the Overview's Income figure.</param>
public sealed record EarningsPoint(string Month, string Label, decimal Earnings);
