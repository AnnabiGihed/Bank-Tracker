namespace KbcTracker.Domain.Features.Debts.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors creating a repayment plan can fail with, coded <c>RepaymentPlan.Property.Reason</c>.
/// </summary>
public static class RepaymentPlanErrors
{
	#region Static Instances
	/// <summary>
	/// The amount to repay is not positive or has more than two decimals.
	/// </summary>
	public static readonly Error AmountInvalid = new("RepaymentPlan.Amount.Invalid", "The amount to repay must be positive with at most two decimals.");

	/// <summary>
	/// The first date is out of range.
	/// </summary>
	public static readonly Error StartInvalid = new("RepaymentPlan.Start.Invalid", "The first date must lie between the years 2000 and 2200.");

	/// <summary>
	/// Neither or both of the instalment amount and the number of instalments were given.
	/// </summary>
	public static readonly Error InstalmentChoice = new("RepaymentPlan.Instalments.Choice", "Give either the amount of each instalment or the number of instalments, not both.");

	/// <summary>
	/// The instalment amount is not positive, has more than two decimals or exceeds the amount to repay.
	/// </summary>
	public static readonly Error InstalmentAmountInvalid = new("RepaymentPlan.InstalmentAmount.Invalid", "An instalment must be positive, have at most two decimals and not exceed the amount to repay.");

	/// <summary>
	/// The number of instalments is not between 1 and 600, or leaves nothing for the last one.
	/// </summary>
	public static readonly Error CountInvalid = new("RepaymentPlan.Count.Invalid", "The number of instalments must be between 1 and 600 and leave an amount for the last one.");

	/// <summary>
	/// The day of the month is not between 1 and 31, or was given for weekly instalments.
	/// </summary>
	public static readonly Error DayOfMonthInvalid = new("RepaymentPlan.DayOfMonth.Invalid", "The day of the month must be between 1 and 31, and does not apply to weekly instalments.");
	#endregion
}
