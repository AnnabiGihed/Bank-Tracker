using KbcTracker.Domain.Features.Debts.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a debt's repayment plan to the clients, with the derived amount of the last instalment.
/// </summary>
/// <param name="Amount">The total to repay, interest included.</param>
/// <param name="Start">The due date of a single payment, or the first date instalments count from.</param>
/// <param name="Frequency">How often an instalment falls due, or <see langword="null"/> for one payment.</param>
/// <param name="DayOfMonth">The day of the month, or <see langword="null"/>.</param>
/// <param name="InstalmentAmount">The amount of every instalment but the last.</param>
/// <param name="Count">The number of payments.</param>
/// <param name="LastInstalmentAmount">The amount of the last payment, which can differ from the others.</param>
public sealed record RepaymentPlanSnapshot(decimal Amount, DateOnly Start, RepaymentFrequency? Frequency, int? DayOfMonth, decimal InstalmentAmount, int Count,
	decimal LastInstalmentAmount)
{
	#region Public Methods
	/// <summary>Copies a plan.</summary>
	/// <param name="plan">The plan.</param>
	/// <returns>The snapshot.</returns>
	public static RepaymentPlanSnapshot From(RepaymentPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);
		return new(plan.Amount, plan.Start, plan.Frequency, plan.DayOfMonth, plan.InstalmentAmount, plan.Count, plan.LastInstalmentAmount);
	}

	/// <summary>Rebuilds the plan.</summary>
	/// <returns>The plan.</returns>
	public RepaymentPlan ToDomain() => RepaymentPlan.Rehydrate(Amount, Start, Frequency, DayOfMonth, InstalmentAmount, Count);
	#endregion
}
