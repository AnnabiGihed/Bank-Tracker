namespace KbcTracker.Domain.Features.Overview.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The figures of a month that are sums of money in and can be opened as a list of the payments behind
///              them: <see cref="Analysis.Behind"/> answers with exactly the payments the <see cref="MonthSummary"/>
///              figure adds up.
/// </summary>
public enum MonthFigure
{
	/// <summary>
	/// The earnings (<see cref="MonthSummary.Income"/>): money in, in income categories, except refunds.
	/// </summary>
	Earnings,

	/// <summary>
	/// The refunds (<see cref="MonthSummary.Refunds"/>): money in, in the Refunds category and below it.
	/// </summary>
	Refunds,

	/// <summary>
	/// The loan money received (<see cref="MonthSummary.LoanReceived"/>).
	/// </summary>
	LoanReceived,
}
