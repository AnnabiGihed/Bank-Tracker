namespace KbcTracker.Domain.Features.Overview.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The totals of one month, as shown on the Overview tab. Amounts are positive numbers; own transfers
///              are left out.
/// </summary>
/// <param name="Month">The month, as <c>yyyy-MM</c>.</param>
/// <param name="Income">Money in, in income categories.</param>
/// <param name="Fixed">Spending matched to accepted fixed payments.</param>
/// <param name="Variable">Other spending; refunds in an expense category reduce it.</param>
/// <param name="Saved">Money put aside, in savings categories.</param>
/// <param name="ByCategory">Spending per expense category id.</param>
public sealed record MonthSummary(string Month, decimal Income, decimal Fixed, decimal Variable, decimal Saved, IReadOnlyDictionary<string, decimal> ByCategory)
{
	#region Properties
	/// <summary>
	/// Gets all spending: fixed plus variable.
	/// </summary>
	public decimal Spent => Fixed + Variable;

	/// <summary>
	/// Gets what is left: income minus spending and savings.
	/// </summary>
	public decimal Net => Income - Spent - Saved;
	#endregion
}
