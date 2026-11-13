namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One existing part of a split payment, as the split editor starts from it.
/// </summary>
/// <param name="Amount">The part's amount, positive and without the payment's sign.</param>
/// <param name="CategoryId">The part's category.</param>
/// <param name="Label">What the part is, or <see langword="null"/>.</param>
public sealed record SplitPartSeed(decimal Amount, string CategoryId, string? Label);
