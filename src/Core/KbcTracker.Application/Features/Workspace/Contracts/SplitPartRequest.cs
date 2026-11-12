namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One part of a payment the user divides over categories, as sent with the <c>split</c> action.
/// </summary>
/// <param name="Amount">The amount of the part, positive and without the payment's sign.</param>
/// <param name="CategoryId">The category of the part; it must be a spending category.</param>
/// <param name="Label">What the part is, or <see langword="null"/>.</param>
public sealed record SplitPartRequest(decimal Amount, string CategoryId, string? Label = null);
