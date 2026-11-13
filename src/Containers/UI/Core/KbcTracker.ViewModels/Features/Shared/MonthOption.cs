namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One entry of the month selector.
/// </summary>
/// <param name="Value">The month, as <c>yyyy-MM</c>.</param>
/// <param name="Label">The month's English name and year.</param>
public sealed record MonthOption(string Value, string Label);
