namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Overview tab shows for a month: the notice about open items, the five key figures (earnings,
///              spending, fixed payments, savings, what is left), the refunds and loan money apart, the last twelve
///              months, the doughnut of where the money went, the fixed payments with their status and the biggest
///              expenses. Computed as the old page did.
/// </summary>
public sealed class OverviewViewModel
{
	#region Constants
	/// <summary>How many fixed payments and expenses the cards list.</summary>
	private const int CardRows = 6;

	/// <summary>How many months the chart of the last months covers.</summary>
	private const int ChartMonths = 12;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="OverviewViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="session">The bank session, or <see langword="null"/> when not connected; it decides what the empty tab says.</param>
	/// <param name="formats">The formats of amounts, dates and months.</param>
	/// <param name="today">Today's date, which decides whether a fixed payment is due or late.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public OverviewViewModel(Analysis analysis, string month, SessionInfo? session, Formats formats, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		Month = month;
		HasTransactions = analysis.Items.Count > 0;
		EmptyMessage = session is null
			? "Connect your bank to get started: click Connect bank and log in with itsme."
			: "No transactions yet — click Sync.";
		if (!HasTransactions)
		{
			return;
		}

		var current = analysis.MonthData(month);
		var fixedRows = analysis.Fixed.Select(f => (Payment: f, State: f.StatusIn(month, today)))
			.Where(x => x.State.Status is not (FixedStatus.NotStarted or FixedStatus.NotThisMonth) || x.State.Paid > 0).ToList();
		var open = fixedRows.Where(x => x.State.Status is FixedStatus.Due or FixedStatus.Late).ToList();
		var waiting = new WaitingSuggestions(analysis.Suggestions.Count(s => !s.IsSubscription), analysis.Suggestions.Count(s => s.IsSubscription));
		Attention = waiting.Total > 0 || analysis.Review.Count > 0 ? new AttentionNotice(waiting, analysis.Review.Count) : null;
		Kpis =
		[
			new("income", "Income", formats.Money(current.Income), EarningsBreakdown(current, formats), MonthFigure.Earnings),
			new("spent", "Spent", formats.Money(current.Spent), $"fixed {formats.WholeMoney(current.Fixed)} · variable {formats.WholeMoney(current.Variable)}"),
			new("fixed", "Fixed payments", formats.Money(current.Fixed), FixedPaymentsOutlook(open.Count, fixedRows.Count, open.Sum(x => x.Payment.Def.Amount), formats)),
			new("saved", "Saved & invested", formats.Money(current.Saved), "plans, spare change…"),
			new("left", "Left over", formats.Money(current.Net), "earnings − spent − saved"),
		];
		AsideFigures = AsideOf(current, formats);
		Series = [.. analysis.Months.TakeLast(ChartMonths).Select(m => new MonthPoint(m.Month, Formats.ShortMonth(m.Month), m.Income, m.Fixed, m.Variable, m.Saved))];
		DrillDown = new CategoryDrillDown(analysis, current);
		FixedPayments = [.. fixedRows.OrderBy(x => x.State.Status is FixedStatus.Paid or FixedStatus.PaidChanged).ThenBy(x => x.Payment.UsualDay).Take(CardRows)
			.Select(x => new FixedSummaryRow(x.Payment.Def.Name, formats.Money(x.State.Paid > 0 ? x.State.Paid : x.Payment.Def.Amount), FixedStatusPill.Of(x.Payment, x.State, formats)))];
		FixedPaidText = $"{fixedRows.Count(x => x.State.Paid > 0)} of {fixedRows.Count} paid this month";
		NoFixedPaymentsText = waiting.Total > 0 ? "No confirmed fixed payments yet —" : "No confirmed fixed payments yet.";
		Waiting = waiting;
		BiggestExpenses = [.. analysis.Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal) && i.IsSpending).OrderBy(i => i.T.Amount).Take(CardRows)
			.Select(i => new ExpenseRow(formats.Day(i.T.Date), i.Name, i.IsSplit ? null : i.Category.Name, i.IsSplit ? null : i.Category.Color, i.IsSplit ? i.Parts.Count : 0,
				formats.Money(-i.T.Amount)))];
	}
	#endregion

	#region Properties
	/// <summary>Gets the selected month, as <c>yyyy-MM</c>.</summary>
	public string Month { get; }

	/// <summary>Gets a value indicating whether there is any transaction; without one the tab only shows <see cref="EmptyMessage"/>.</summary>
	public bool HasTransactions { get; }

	/// <summary>Gets what the empty tab says: to connect a bank, or to sync.</summary>
	public string EmptyMessage { get; }

	/// <summary>Gets the banner that points to the fixed payment and subscription suggestions and the payees to review, or <see langword="null"/> when there are none.</summary>
	public AttentionNotice? Attention { get; }

	/// <summary>Gets the five key figures: income, spent, fixed payments, saved and invested, left over.</summary>
	public IReadOnlyList<Kpi> Kpis { get; } = [];

	/// <summary>Gets the figures counted in no total: the month's refunds and the loan money received, each only when not zero.</summary>
	public IReadOnlyList<Kpi> AsideFigures { get; } = [];

	/// <summary>Gets the last twelve months, oldest first.</summary>
	public IReadOnlyList<MonthPoint> Series { get; } = [];

	/// <summary>Gets the data of the doughnut of where the month's money went.</summary>
	public CategoryDrillDown? DrillDown { get; }

	/// <summary>Gets the month's fixed payments, not yet paid ones first, at most six.</summary>
	public IReadOnlyList<FixedSummaryRow> FixedPayments { get; } = [];

	/// <summary>Gets the words that tell how many of the month's fixed payments are paid.</summary>
	public string FixedPaidText { get; } = string.Empty;

	/// <summary>Gets the fixed payment and subscription suggestions that wait for the owner.</summary>
	public WaitingSuggestions Waiting { get; } = new(0, 0);

	/// <summary>Gets what the fixed payments card says when none is confirmed; it ends in a dash when <see cref="Waiting"/> has suggestions to point to.</summary>
	public string NoFixedPaymentsText { get; } = string.Empty;

	/// <summary>Gets the month's biggest expenses, largest first, at most six.</summary>
	public IReadOnlyList<ExpenseRow> BiggestExpenses { get; } = [];
	#endregion

	#region Private Helpers
	/// <summary>Words the line under the income figure: the salary and the other earnings, each only when not zero.</summary>
	private static string EarningsBreakdown(MonthSummary month, Formats formats)
	{
		var parts = new List<string>();
		if (month.Salary != 0)
		{
			parts.Add($"Salary {formats.WholeMoney(month.Salary)}");
		}

		if (month.Income - month.Salary != 0)
		{
			parts.Add($"Other {formats.WholeMoney(month.Income - month.Salary)}");
		}

		return parts.Count > 0 ? string.Join(" · ", parts) : "no earnings yet";
	}

	/// <summary>Builds the refunds and loan-received figures, which no total includes.</summary>
	private static List<Kpi> AsideOf(MonthSummary month, Formats formats)
	{
		var figures = new List<Kpi>();
		if (month.Refunds != 0)
		{
			figures.Add(new("refunds", "Refunds", formats.Money(month.Refunds), "not counted in income or spending", MonthFigure.Refunds));
		}

		if (month.LoanReceived != 0)
		{
			figures.Add(new("loan", "Loan received", formats.Money(month.LoanReceived), "not income", MonthFigure.LoanReceived));
		}

		return figures;
	}

	/// <summary>Words the line under the fixed-payments figure: what is still to come, or that everything is paid or none is confirmed yet.</summary>
	private static string FixedPaymentsOutlook(int stillToCome, int tracked, decimal amountToCome, Formats formats)
	{
		if (stillToCome > 0)
		{
			return $"{stillToCome} still to come · {formats.WholeMoney(amountToCome)}";
		}

		return tracked == 0 ? "none confirmed yet" : "all paid";
	}
	#endregion
}
