namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One line of a payment's split as stored in the <c>TxnSplits</c> JSON column. A line can later gain
///              fields, such as where it came from, without a schema change.
/// </summary>
/// <param name="Amount">The amount of the line, positive.</param>
/// <param name="CategoryId">The category id.</param>
/// <param name="Label">The label, or <see langword="null"/>.</param>
internal sealed record StoredSplitLine(decimal Amount, string CategoryId, string? Label);
