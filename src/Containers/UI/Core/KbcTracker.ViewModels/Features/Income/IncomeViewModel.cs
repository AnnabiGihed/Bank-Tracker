namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Income tab shows for a month: the earnings (salary and other income), the refunds and the loan
///              money received, each with its payments, the earnings of the last twelve months and the earnings by
///              payer over those months. Every section lists <see cref="Analysis.Behind"/>, the rule the Overview's
///              figures add up with, so each section's total is exactly its Overview figure.
/// </summary>
public sealed class IncomeViewModel
{
	#region Constants
	/// <summary>How many months the chart and the payer list cover.</summary>
	private const int WindowMonths = 12;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="IncomeViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="formats">The formats of amounts, dates and months.</param>
	/// <param name="focus">The figure whose section is shown first (the click-through of the Overview), or <see langword="null"/> for none.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public IncomeViewModel(Analysis analysis, string month, Formats formats, MonthFigure? focus = null)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		MonthLabel = Formats.MonthName(month);
		Focus = focus is { } figure ? MonthFigureNames.ToQuery(figure) : null;
		var earnings = analysis.Behind(month, MonthFigure.Earnings);
		var refunds = analysis.Behind(month, MonthFigure.Refunds);
		var loan = analysis.Behind(month, MonthFigure.LoanReceived);
		Sections =
		[
			Section(MonthFigure.Earnings, earnings, "No earnings this month.", formats,
				Group("Salary", [.. earnings.Where(analysis.IsSalary)], analysis, formats),
				Group("Other income", [.. earnings.Where(i => !analysis.IsSalary(i))], analysis, formats)),
			Section(MonthFigure.Refunds, refunds, "No refunds this month.", formats, Group("Refunds", refunds, analysis, formats)),
			Section(MonthFigure.LoanReceived, loan, "No loan money received this month.", formats, Group("Loan received", loan, analysis, formats)),
		];
		var window = analysis.Months.TakeLast(WindowMonths).ToList();
		Series = [.. window.Select(m => new EarningsPoint(m.Month, Formats.ShortMonth(m.Month), m.Income))];
		Payers = [.. window.SelectMany(m => analysis.Behind(m.Month, MonthFigure.Earnings)).GroupBy(i => i.SeriesKey, StringComparer.Ordinal)
			.Select(g => new PayerRow(g.First().Name, g.Sum(i => i.T.Amount), formats.Money(g.Sum(i => i.T.Amount)), $"{formats.Number(g.Count())} payment(s)"))
			.OrderByDescending(p => p.Total).ThenBy(p => p.Name, formats.NameComparer)];
		CategoryGroups = CategoryOptions.Grouped(analysis, formats);
	}
	#endregion

	#region Properties
	/// <summary>Gets the selected month's English name and year.</summary>
	public string MonthLabel { get; }

	/// <summary>Gets the id of the section to show first, or <see langword="null"/> for none.</summary>
	public string? Focus { get; }

	/// <summary>Gets the sections of the month: earnings, refunds and loan received, in that order.</summary>
	public IReadOnlyList<IncomeSection> Sections { get; }

	/// <summary>Gets the earnings of the last twelve months, oldest first.</summary>
	public IReadOnlyList<EarningsPoint> Series { get; }

	/// <summary>Gets what each payer paid in earnings over those months, the largest first.</summary>
	public IReadOnlyList<PayerRow> Payers { get; }

	/// <summary>Gets every category grouped by type, for the picker of a payment.</summary>
	public IReadOnlyList<CategoryGroup> CategoryGroups { get; }
	#endregion

	#region Private Helpers
	/// <summary>Builds a section from the payments behind its figure and its groups, leaving out empty groups.</summary>
	private static IncomeSection Section(MonthFigure figure, IReadOnlyList<TxnInfo> payments, string emptyText, Formats formats, params IncomeGroup[] groups)
	{
		var total = payments.Sum(i => i.T.Amount);
		return new(MonthFigureNames.ToQuery(figure), MonthFigureNames.Label(figure), total, formats.Money(total), [.. groups.Where(g => g.Rows.Count > 0)], emptyText);
	}

	/// <summary>Builds a group of payments with its total.</summary>
	private static IncomeGroup Group(string title, IReadOnlyList<TxnInfo> payments, Analysis analysis, Formats formats) =>
		new(title, formats.Money(payments.Sum(i => i.T.Amount)), [.. payments.Select(i => ToRow(i, analysis, formats))]);

	/// <summary>Builds the row of one payment.</summary>
	private static IncomeRow ToRow(TxnInfo item, Analysis analysis, Formats formats) =>
		new(item.T.Id.Value, item.SeriesKey, formats.Day(item.T.Date), item.Name, item.T.Description, item.T.Amount, formats.Money(item.T.Amount),
			item.Category.Id.Value, analysis.CategoryById(item.Category.Id.Value).Name, item.CategoryConfirmed);
	#endregion
}
