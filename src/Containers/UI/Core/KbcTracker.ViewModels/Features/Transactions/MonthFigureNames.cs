namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The words a <see cref="MonthFigure"/> goes by: its value in the <c>show</c> query of the Transactions
///              tab (<c>earnings</c>, <c>refunds</c>, <c>loan-received</c>) and the label shown for the active filter.
/// </summary>
public static class MonthFigureNames
{
	#region Public Methods
	/// <summary>Gets the <c>show</c> query value of a figure.</summary>
	/// <param name="figure">The figure.</param>
	/// <returns>The query value.</returns>
	public static string ToQuery(MonthFigure figure) => figure switch
	{
		MonthFigure.Earnings => "earnings",
		MonthFigure.Refunds => "refunds",
		MonthFigure.LoanReceived => "loan-received",
		_ => throw new ArgumentOutOfRangeException(nameof(figure), figure, "Unknown month figure."),
	};

	/// <summary>Reads a <c>show</c> query value.</summary>
	/// <param name="query">The query value, or <see langword="null"/>.</param>
	/// <returns>The figure, or <see langword="null"/> when the value is empty or unknown.</returns>
	public static MonthFigure? Parse(string? query) => query switch
	{
		"earnings" => MonthFigure.Earnings,
		"refunds" => MonthFigure.Refunds,
		"loan-received" => MonthFigure.LoanReceived,
		_ => null,
	};

	/// <summary>Gets the label of a figure, as the Overview names it.</summary>
	/// <param name="figure">The figure.</param>
	/// <returns>The label.</returns>
	public static string Label(MonthFigure figure) => figure switch
	{
		MonthFigure.Earnings => "Income",
		MonthFigure.Refunds => "Refunds",
		MonthFigure.LoanReceived => "Loan received",
		_ => throw new ArgumentOutOfRangeException(nameof(figure), figure, "Unknown month figure."),
	};
	#endregion
}
