namespace KbcTracker.Domain.Features.Splits.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One part of a split payment, shaped like a receipt line: an amount, the category it belongs to and
///              an optional label.
/// </summary>
/// <param name="Amount">The amount of the part, positive and without the payment's sign.</param>
/// <param name="CategoryId">The category of the part.</param>
/// <param name="Label">What the part is, for example the receipt line's text, or <see langword="null"/>.</param>
public sealed record SplitLine(decimal Amount, CategoryId CategoryId, string? Label);
