namespace KbcTracker.Domain.Features.Debts.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Finds the repayments of every debt among the payments and works out where each debt stands (ADR-0010).
///
///              Matching: a bank loan takes the payments of its <c>LOAN:</c> series (any loan instalment when it has none);
///              a personal loan takes transfers out to the lender's IBAN, or to a payee with the lender's normalised name
///              when it has no IBAN. Only payments dated on or after the day the money was borrowed are taken, oldest
///              first, until the amount to repay is covered. A payment counts for one debt only;
///              payments the owner added always count and payments the owner excluded never do.
///
///              Immutable; computed again whenever the analysis is built.
/// </summary>
public sealed class DebtLedger
{
	#region Constants
	/// <summary>
	/// The number of days ahead a payment counts as due soon.
	/// </summary>
	public const int DueSoonDays = 7;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises the ledger from computed parts.
	/// </summary>
	private DebtLedger(IReadOnlyList<DebtStatus> debts, IReadOnlyList<TxnInfo> unplanned, IReadOnlySet<TxnId> repaymentIds)
	{
		Debts = debts;
		Unplanned = unplanned;
		RepaymentIds = repaymentIds;
	}
	#endregion

	#region Properties
	/// <summary>
	/// Gets the status of every debt, in the order the debts were given.
	/// </summary>
	public IReadOnlyList<DebtStatus> Debts { get; }

	/// <summary>
	/// Gets the loan credits (money in under Loan received) no debt tracks, in the order of the payments given.
	/// </summary>
	public IReadOnlyList<TxnInfo> Unplanned { get; }

	/// <summary>
	/// Gets the ids of the payments that count as a repayment of some debt.
	/// </summary>
	public IReadOnlySet<TxnId> RepaymentIds { get; }
	#endregion

	#region Factory Methods
	/// <summary>
	/// Computes the ledger.
	/// </summary>
	/// <param name="debts">The debts the owner tracks.</param>
	/// <param name="items">The classified payments.</param>
	/// <param name="today">The reference date for overdue and due-soon payments.</param>
	/// <param name="tree">The category tree, to tell which credits are loan money.</param>
	/// <returns>The ledger.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static DebtLedger Compute(IEnumerable<Debt> debts, IEnumerable<TxnInfo> items, DateOnly today, CategoryTree tree)
	{
		ArgumentNullException.ThrowIfNull(debts);
		ArgumentNullException.ThrowIfNull(items);
		ArgumentNullException.ThrowIfNull(tree);
		var debtList = debts.ToList();
		var itemList = items.ToList();
		var byId = new Dictionary<TxnId, TxnInfo>();
		foreach (var item in itemList)
		{
			byId.TryAdd(item.T.Id, item);
		}

		var addedByHand = debtList.SelectMany(d => d.IncludedPayments).ToHashSet();
		var taken = new HashSet<TxnId>();
		var statuses = new List<DebtStatus>(debtList.Count);
		foreach (var debt in debtList)
		{
			var payments = Match(debt, itemList, byId, addedByHand, taken);
			statuses.Add(StatusOf(debt, payments, today));
		}

		var tracked = debtList.Where(d => d.ReceivedPaymentId is not null).Select(d => d.ReceivedPaymentId!).ToHashSet();
		var unplanned = itemList.Where(i => i.T.Amount > 0 && !i.IsSplit && !tracked.Contains(i.T.Id) && IsLoanMoney(i.Category, tree)).ToList();
		return new DebtLedger(statuses, unplanned, statuses.SelectMany(s => s.Payments).Select(p => p.T.Id).ToHashSet());
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Finds the repayments of one debt: those added by hand, then the matching payments oldest first until covered.
	/// </summary>
	private static List<TxnInfo> Match(Debt debt, List<TxnInfo> items, Dictionary<TxnId, TxnInfo> byId, HashSet<TxnId> addedByHand, HashSet<TxnId> taken)
	{
		var payments = debt.IncludedPayments.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
		var paid = payments.Sum(p => Math.Abs(p.T.Amount));
		var from = debt.BorrowedOn;
		var candidates = items
			.Where(i => i.T.Amount < 0 && i.T.Date >= from && !addedByHand.Contains(i.T.Id) && !taken.Contains(i.T.Id)
				&& !debt.ExcludedPayments.Contains(i.T.Id) && IsRepaymentOf(debt, i))
			.OrderBy(i => i.T.Date).ThenBy(i => i.T.Id.Value, StringComparer.Ordinal);
		foreach (var candidate in candidates)
		{
			if (paid >= debt.AmountToRepay)
			{
				break;
			}

			payments.Add(candidate);
			taken.Add(candidate.T.Id);
			paid += -candidate.T.Amount;
		}

		return [.. payments.OrderBy(p => p.T.Date).ThenBy(p => p.T.Id.Value, StringComparer.Ordinal)];
	}

	/// <summary>
	/// Determines whether a payment is the kind of payment that repays the debt.
	/// </summary>
	private static bool IsRepaymentOf(Debt debt, TxnInfo item)
	{
		if (debt.Kind == DebtKind.BankLoan)
		{
			return debt.SeriesKey is null
				? item.Kind == Kind.Loan
				: string.Equals(item.SeriesKey, debt.SeriesKey, StringComparison.Ordinal);
		}

		if (item.Kind is not (Kind.Transfer or Kind.Other))
		{
			return false;
		}

		return debt.Iban is not null
			? string.Equals(item.T.CounterpartyIban, debt.Iban, StringComparison.OrdinalIgnoreCase)
			: string.Equals(Analysis.Norm(item.T.Counterparty), Analysis.Norm(debt.Lender), StringComparison.Ordinal);
	}

	/// <summary>
	/// Lays the repayments over the schedule and sums the situation up.
	/// </summary>
	private static DebtStatus StatusOf(Debt debt, List<TxnInfo> payments, DateOnly today)
	{
		var paid = payments.Sum(p => Math.Abs(p.T.Amount));
		var cover = debt.AmountToRepay;
		var schedule = Lay(debt.Plan, paid);
		var late = schedule.Where(r => r.Due < today && !r.IsPaid).ToList();
		var next = schedule.FirstOrDefault(r => r.Due >= today && !r.IsPaid);
		var state = StateOf(debt, paid, late.Count, next, today);
		return new DebtStatus(debt, state, paid, Math.Max(0, cover - paid), Math.Max(0, paid - cover), next?.Due,
			next is null ? null : next.Amount - next.Covered, late.Sum(r => r.Amount - r.Covered), late.Count > 0 ? late[0].Due : null, schedule, payments);
	}

	/// <summary>
	/// Chooses the state: the first that applies of overpaid, repaid, no plan, overdue and due soon.
	/// </summary>
	private static DebtState StateOf(Debt debt, decimal paid, int lateCount, ScheduleRow? next, DateOnly today)
	{
		if (paid > debt.AmountToRepay)
		{
			return DebtState.Overpaid;
		}

		if (paid == debt.AmountToRepay)
		{
			return DebtState.Repaid;
		}

		if (debt.Plan is null)
		{
			return DebtState.NoPlan;
		}

		if (lateCount > 0)
		{
			return DebtState.Overdue;
		}

		return next is not null && next.Due.DayNumber - today.DayNumber <= DueSoonDays ? DebtState.DueSoon : DebtState.OnTrack;
	}

	/// <summary>
	/// Fills the plan's payments in order with what was repaid.
	/// </summary>
	private static List<ScheduleRow> Lay(RepaymentPlan? plan, decimal paid)
	{
		var rows = new List<ScheduleRow>();
		var left = paid;
		foreach (var payment in plan?.Schedule() ?? [])
		{
			var covered = Math.Min(payment.Amount, left);
			rows.Add(new ScheduleRow(payment.Due, payment.Amount, covered));
			left -= covered;
		}

		return rows;
	}

	/// <summary>
	/// Determines whether a category is Loan received or lies below it.
	/// </summary>
	private static bool IsLoanMoney(Category category, CategoryTree tree) =>
		category.Id == CategoryId.LoanReceived || tree.Ancestors(category.Id.Value).Any(a => a.Id == CategoryId.LoanReceived);
	#endregion
}
