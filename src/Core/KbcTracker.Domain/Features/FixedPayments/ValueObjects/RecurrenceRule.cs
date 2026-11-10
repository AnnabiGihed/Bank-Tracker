namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The amount and rhythm a fixed payment is expected with, and how far a payment may differ from the
///              amount and still be matched by its shop words.
/// </summary>
/// <param name="Amount">The usual amount, as a positive number.</param>
/// <param name="TolerancePct">The allowed difference with <paramref name="Amount"/> for matching by shop words, in
/// percent (energy bills vary more than a streaming subscription).</param>
/// <param name="CadenceDays">The number of days between payments: 30 monthly, 91 quarterly, 365 yearly.</param>
public readonly record struct RecurrenceRule(decimal Amount, decimal TolerancePct, int CadenceDays);
