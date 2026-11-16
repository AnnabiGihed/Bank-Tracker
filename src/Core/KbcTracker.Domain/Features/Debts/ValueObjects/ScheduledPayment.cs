namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment a repayment plan asks for: the date it falls due and its amount.
/// </summary>
/// <param name="Due">The due date.</param>
/// <param name="Amount">The amount, positive.</param>
public sealed record ScheduledPayment(DateOnly Due, decimal Amount);
