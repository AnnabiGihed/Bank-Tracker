namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment a suggestion was found from, shown as a small chip: its date and amount.
/// </summary>
/// <param name="Text">The date and amount.</param>
/// <param name="Tooltip">The bank's description of the payment.</param>
public sealed record SuggestionChip(string Text, string Tooltip);
