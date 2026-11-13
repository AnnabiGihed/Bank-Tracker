namespace KbcTracker.Domain.Features.Overview.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The totals of one month, as shown on the Overview tab. Amounts are positive numbers; own transfers
///              are left out.
/// </summary>
/// <param name="Month">The month, as <c>yyyy-MM</c>.</param>
/// <param name="Income">Earnings: money in, in income categories, except refunds.</param>
/// <param name="Fixed">Spending matched to accepted fixed payments.</param>
/// <param name="Variable">Other spending; refunds in an expense category reduce it.</param>
/// <param name="Saved">Money put aside, in savings categories.</param>
/// <param name="ByCategory">Spending per expense category id.</param>
/// <param name="Salary">The part of <paramref name="Income"/> in the Salary category and below it.</param>
/// <param name="Refunds">Money in, in the built-in Refunds category and below it; counted in no total.</param>
/// <param name="LoanReceived">Money in, in the built-in Loan received category; counted in no total.</param>
public sealed record MonthSummary(string Month, decimal Income, decimal Fixed, decimal Variable, decimal Saved, IReadOnlyDictionary<string, decimal> ByCategory,
	decimal Salary = 0, decimal Refunds = 0, decimal LoanReceived = 0)
{
	#region Properties
	/// <summary>
	/// Gets all spending: fixed plus variable.
	/// </summary>
	public decimal Spent => Fixed + Variable;

	/// <summary>
	/// Gets what is left: earnings minus spending and savings; refunds and loan money are not part of it.
	/// </summary>
	public decimal Net => Income - Spent - Saved;
	#endregion
}
