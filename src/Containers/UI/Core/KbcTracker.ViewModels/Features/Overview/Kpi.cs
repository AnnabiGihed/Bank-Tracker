namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One key figure of the Overview tab: its name, the amount and a line that explains it.
/// </summary>
/// <param name="Key">Which figure it is: <c>income</c>, <c>spent</c>, <c>fixed</c>, <c>saved</c> or <c>left</c>, or <c>refunds</c> or <c>loan</c> for the figures counted in no total; it picks the colour.</param>
/// <param name="Label">The figure's name.</param>
/// <param name="Value">The amount, formatted.</param>
/// <param name="Sub">The explanation under the figure.</param>
/// <param name="Figure">The figure of the month whose payments the card opens in Income, or <see langword="null"/> when the card is no link.</param>
public sealed record Kpi(string Key, string Label, string Value, string Sub, MonthFigure? Figure = null);
