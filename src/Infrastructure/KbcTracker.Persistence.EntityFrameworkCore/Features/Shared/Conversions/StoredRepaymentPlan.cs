namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A repayment plan as stored inside a <see cref="StoredDebt"/>.
/// </summary>
/// <param name="Amount">The total to repay.</param>
/// <param name="Start">The due date of a single payment, or the first date.</param>
/// <param name="Frequency">The name of the <see cref="RepaymentFrequency"/>, or <see langword="null"/> for one payment.</param>
/// <param name="DayOfMonth">The day of the month, or <see langword="null"/>.</param>
/// <param name="InstalmentAmount">The amount of every instalment but the last.</param>
/// <param name="Count">The number of payments.</param>
internal sealed record StoredRepaymentPlan(decimal Amount, DateOnly Start, string? Frequency, int? DayOfMonth, decimal InstalmentAmount, int Count);
