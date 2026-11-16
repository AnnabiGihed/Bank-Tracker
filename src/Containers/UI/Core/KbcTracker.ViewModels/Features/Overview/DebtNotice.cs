namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Overview's notice says about debts (ADR-0010): payments due within a week, repayments overdue and
///              loan credits without a plan. Each line links to the Debts tab.
/// </summary>
/// <param name="Lines">The lines, due payments first, then the overdue repayments, then the credits without a plan.</param>
public sealed record DebtNotice(IReadOnlyList<string> Lines)
{
	#region Properties
	/// <summary>Gets the text of the link every line carries.</summary>
	public static string CheckText => "Check debts";
	#endregion

	#region Factory Methods
	/// <summary>Builds the notice from the debts' status.</summary>
	/// <param name="analysis">The analysis that holds the debts and the credits without a debt.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <param name="today">Today's date, to count the days until a payment is due.</param>
	/// <returns>The notice, or <see langword="null"/> when there is nothing to say.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static DebtNotice? From(Analysis analysis, Formats formats, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(formats);
		var lines = new List<string>();
		foreach (var status in analysis.Debts.Where(s => s.State == DebtState.DueSoon && s.NextDueDate is not null && s.NextDueAmount is not null))
		{
			lines.Add($"{formats.Money(status.NextDueAmount!.Value)} to {status.Debt.Lender} due {DueWords(status.NextDueDate!.Value.DayNumber - today.DayNumber)}");
		}

		var overdue = analysis.Debts.Sum(s => s.Schedule.Count(r => r.Due < today && !r.IsPaid));
		if (overdue > 0)
		{
			lines.Add(overdue == 1 ? "1 repayment overdue" : $"{formats.Number(overdue)} repayments overdue");
		}

		var unplanned = analysis.UnplannedLoans.Count;
		if (unplanned > 0)
		{
			lines.Add(unplanned == 1 ? "1 loan credit without a plan" : $"{formats.Number(unplanned)} loan credits without a plan");
		}

		return lines.Count == 0 ? null : new DebtNotice(lines);
	}
	#endregion

	#region Private Helpers
	/// <summary>Words the days until a payment is due.</summary>
	private static string DueWords(int days) => days switch
	{
		<= 0 => "today",
		1 => "tomorrow",
		_ => $"in {days} days",
	};
	#endregion
}
