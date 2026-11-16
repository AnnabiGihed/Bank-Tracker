namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One line of a debt's schedule with how much of it the repayments found so far cover. Repayments fill
///              the lines in order, oldest first.
/// </summary>
/// <param name="Due">The due date.</param>
/// <param name="Amount">The amount due.</param>
/// <param name="Covered">The part of <paramref name="Amount"/> already repaid.</param>
public sealed record ScheduleRow(DateOnly Due, decimal Amount, decimal Covered)
{
	#region Properties
	/// <summary>
	/// Gets a value indicating whether the line is repaid in full.
	/// </summary>
	public bool IsPaid => Covered >= Amount;
	#endregion
}
