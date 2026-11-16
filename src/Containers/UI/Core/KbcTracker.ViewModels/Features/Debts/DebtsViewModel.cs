namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Debts tab shows (ADR-0010): a card per debt with its progress, schedule and repayments, active
///              debts first (the most urgent first), then the repaid ones, and the loan credits without a debt. It also opens
///              the repayment plan form for a credit, a debt or a loan that has no credit.
/// </summary>
public sealed class DebtsViewModel
{
	#region Constants
	/// <summary>The most debits the "Add a payment" picker offers, the newest first.</summary>
	private const int MaxAddablePayments = 200;

	/// <summary>The number of percent of a fully repaid debt.</summary>
	private const int FullPercent = 100;
	#endregion

	#region Fields
	/// <summary>The analysis the cards were built from.</summary>
	private readonly Analysis _analysis;

	/// <summary>The formats of amounts and dates.</summary>
	private readonly Formats _formats;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="DebtsViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="formats">The formats of amounts and dates.</param>
	/// <param name="today">Today's date, which decides whether a schedule line is overdue.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public DebtsViewModel(Analysis analysis, Formats formats, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(formats);
		_analysis = analysis;
		_formats = formats;
		var counted = analysis.Debts.SelectMany(d => d.Payments).Select(p => p.T.Id).ToHashSet();
		var addable = analysis.Items.Where(i => i.T.Amount < 0 && !counted.Contains(i.T.Id)).OrderByDescending(i => i.T.Date)
			.ThenBy(i => i.T.Id.Value, StringComparer.Ordinal).Take(MaxAddablePayments)
			.Select(i => new PaymentOption(i.T.Id.Value, $"{formats.ShortDate(i.T.Date)} · {i.Name} · {formats.Money(-i.T.Amount)}")).ToList();
		Cards = [.. analysis.Debts.OrderBy(s => IsActive(s) ? 0 : 1).ThenBy(s => s.OldestMissedDate ?? s.NextDueDate ?? DateOnly.MaxValue)
			.ThenBy(s => s.Debt.Lender, formats.NameComparer).Select(s => ToCard(s, addable, today))];
		Unplanned = [.. analysis.UnplannedLoans.Select(i => new UnplannedCredit(i.T.Id.Value, formats.ShortDate(i.T.Date), i.Name, formats.Money(i.T.Amount), i.Category.Name))];
	}
	#endregion

	#region Properties
	/// <summary>Gets a card per debt, the debts still being repaid first, the most urgent of them first.</summary>
	public IReadOnlyList<DebtCard> Cards { get; }

	/// <summary>Gets the loan credits no debt tracks.</summary>
	public IReadOnlyList<UnplannedCredit> Unplanned { get; }

	/// <summary>Gets the message shown when there is neither a debt nor a credit to plan.</summary>
	public static string EmptyMessage => "No debts yet. Put a payment in under Loan received or Borrowed from people, or add a loan yourself.";

	/// <summary>Gets the question asked before a debt stops being tracked.</summary>
	public static string RemovePrompt => "Stop tracking this debt? Its repayments become ordinary payments again.";
	#endregion

	#region Public Methods
	/// <summary>Opens the plan form for a loan credit that has no debt yet.</summary>
	/// <param name="analysis">The analysis.</param>
	/// <param name="creditId">The credit's transaction id.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>The form, or <see langword="null"/> when the credit is not an unplanned loan credit.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static RepaymentPlanEdit? PlanForCredit(Analysis analysis, string creditId, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(creditId);
		ArgumentNullException.ThrowIfNull(formats);
		var credit = analysis.UnplannedLoans.FirstOrDefault(i => string.Equals(i.T.Id.Value, creditId, StringComparison.Ordinal));
		return credit is null ? null : RepaymentPlanEdit.ForCredit(analysis, credit, formats);
	}

	/// <summary>Opens the plan form for an unplanned credit.</summary>
	/// <param name="creditId">The credit's transaction id.</param>
	/// <returns>The form, or <see langword="null"/> when the credit is not an unplanned loan credit (any more).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="creditId"/> is <see langword="null"/>.</exception>
	public RepaymentPlanEdit? PlanForCredit(string creditId) => PlanForCredit(_analysis, creditId, _formats);

	/// <summary>Opens the plan form for an existing debt.</summary>
	/// <param name="debtId">The debt's identifier.</param>
	/// <returns>The form, or <see langword="null"/> when the debt does not exist (any more).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="debtId"/> is <see langword="null"/>.</exception>
	public RepaymentPlanEdit? PlanForDebt(string debtId)
	{
		ArgumentNullException.ThrowIfNull(debtId);
		var status = _analysis.Debts.FirstOrDefault(s => string.Equals(s.Debt.Id.Value, debtId, StringComparison.Ordinal));
		return status is null ? null : RepaymentPlanEdit.ForDebt(_analysis, status.Debt, _formats);
	}

	/// <summary>Opens the plan form for a loan that has no credit in the bank's history.</summary>
	/// <returns>An empty form.</returns>
	public RepaymentPlanEdit PlanForNewDebt() => RepaymentPlanEdit.ForNewDebt(_analysis, _formats);
	#endregion

	#region Private Helpers
	/// <summary>Determines whether a debt is still being repaid.</summary>
	private static bool IsActive(DebtStatus status) => status.State is not (DebtState.Repaid or DebtState.Overpaid);

	/// <summary>Builds the card of one debt.</summary>
	private DebtCard ToCard(DebtStatus status, List<PaymentOption> addable, DateOnly today)
	{
		var debt = status.Debt;
		var added = debt.IncludedPayments;
		return new DebtCard(debt.Id.Value, debt.Lender, debt.Kind == DebtKind.BankLoan ? "Bank loan" : "Personal loan", PillOf(status),
			(int)Math.Round(status.Progress * FullPercent), $"{_formats.Money(status.Paid)} of {_formats.Money(debt.AmountToRepay)}", _formats.Money(status.Remaining),
			NextText(status), debt.Plan is null ? null : RepaymentPlanText.Describe(debt.Plan, _formats), IsActive(status),
			[.. status.Schedule.Select(r => LineOf(r, today))],
			[.. status.Payments.Select(p => new DebtPaymentRow(p.T.Id.Value, _formats.ShortDate(p.T.Date), p.Name, _formats.Money(-p.T.Amount), added.Contains(p.T.Id),
				DecisionRequests.UnmatchRepayment(debt.Id.Value, p.T.Id.Value)))],
			addable, DecisionRequests.RemoveDebt(debt.Id.Value));
	}

	/// <summary>Words the status of a debt.</summary>
	private DebtPill PillOf(DebtStatus status) => status.State switch
	{
		DebtState.Overdue => new($"Overdue {_formats.Money(status.OverdueAmount)}", PillTone.Bad),
		DebtState.DueSoon => new("Due soon", PillTone.Due),
		DebtState.OnTrack => new("On track", PillTone.Ok),
		DebtState.Repaid => new("Repaid", PillTone.Ok),
		DebtState.Overpaid => new($"Overpaid {_formats.Money(status.Overpaid)}", PillTone.Due),
		_ => new("No plan yet", PillTone.Nil),
	};

	/// <summary>Words the next payment, or gives <see langword="null"/> when none is due.</summary>
	private string? NextText(DebtStatus status) => status.NextDueDate is { } due && status.NextDueAmount is { } amount
		? $"{_formats.Money(amount)} on {_formats.ShortDate(due)}"
		: null;

	/// <summary>Words one line of the schedule.</summary>
	private DebtScheduleLine LineOf(ScheduleRow row, DateOnly today)
	{
		var amount = _formats.Money(row.Amount);
		if (row.IsPaid)
		{
			return new(_formats.ShortDate(row.Due), amount, "Paid", PillTone.Ok);
		}

		var left = row.Covered > 0 ? $"Partly paid, {_formats.Money(row.Amount - row.Covered)} left" : null;
		if (row.Due >= today)
		{
			return new(_formats.ShortDate(row.Due), amount, left ?? "Upcoming", PillTone.Nil);
		}

		return new(_formats.ShortDate(row.Due), amount, left is null ? "Overdue" : $"Overdue, {left}", PillTone.Bad);
	}
	#endregion
}
