namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One fixed payment on the Overview tab's card: its name, the amount (what was paid, otherwise the usual
///              amount) and its status in the month.
/// </summary>
/// <param name="Name">The fixed payment's name.</param>
/// <param name="AmountText">The amount, formatted.</param>
/// <param name="Status">Its status in the month.</param>
public sealed record FixedSummaryRow(string Name, string AmountText, FixedStatusPill Status);
