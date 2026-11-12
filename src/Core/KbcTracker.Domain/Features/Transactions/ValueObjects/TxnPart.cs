namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The share of a payment that belongs to one category: the payment's whole amount when it is not
///              split, otherwise one line of its split. Every total by category adds up parts, never payments.
/// </summary>
/// <param name="Category">The category of the part.</param>
/// <param name="Amount">The amount of the part, signed like the payment (negative for money out).</param>
/// <param name="Label">What the part is, or <see langword="null"/>.</param>
public sealed record TxnPart(Category Category, decimal Amount, string? Label);
