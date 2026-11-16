using KbcTracker.Domain.Features.Debts.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How the owner repays a debt, as sent with the <c>addDebt</c> and <c>updateDebt</c> actions (ADR-0010): one
///              payment on a date, or instalments given by their amount or their number.
/// </summary>
/// <param name="Amount">The total to repay, interest included; positive with at most two decimals.</param>
/// <param name="Start">The due date of the single payment, or the first date instalments count from.</param>
/// <param name="Frequency">How often an instalment falls due, or <see langword="null"/> for one payment.</param>
/// <param name="DayOfMonth">The day of the month (1 to 31) of monthly, quarterly and yearly instalments, or
/// <see langword="null"/> for the day of <paramref name="Start"/>; not for weekly instalments.</param>
/// <param name="InstalmentAmount">The amount of each instalment; give this or <paramref name="Count"/>, not both.</param>
/// <param name="Count">The number of instalments (1 to 600); give this or <paramref name="InstalmentAmount"/>, not both.</param>
public sealed record RepaymentPlanRequest(decimal Amount, DateOnly Start, RepaymentFrequency? Frequency = null, int? DayOfMonth = null,
	decimal? InstalmentAmount = null, int? Count = null);
