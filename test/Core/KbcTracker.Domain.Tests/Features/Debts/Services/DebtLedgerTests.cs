namespace KbcTracker.Domain.Tests.Features.Debts.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins how repayments are matched to debts and how a debt's status is worked out (ADR-0010): matching by
///              IBAN, name and loan series, the payout date, the coverage cap, one debt per payment, payments added or
///              excluded by hand, every status, and the loan credits no debt tracks.
///
///              Runs the whole <see cref="Analysis"/> over invented transactions and the default category tree, with a
///              fixed reference date.
/// </summary>
public sealed class DebtLedgerTests
{
	#region Test Infrastructure
	/// <summary>
	/// The fixed "today" of every analysis in this class.
	/// </summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>
	/// The IBAN of the invented friend.
	/// </summary>
	private const string FriendIban = "BE00111122223333";

	/// <summary>
	/// Four monthly instalments of 250 from 5 October (5 Oct, 5 Nov, 5 Dec, 5 Jan).
	/// </summary>
	private static RepaymentPlan MonthlyPlan() =>
		RepaymentPlan.Instalments(1000m, new DateOnly(2026, 10, 5), RepaymentFrequency.Monthly, null, 250m, null).Value;

	/// <summary>
	/// Creates a personal loan from the invented friend.
	/// </summary>
	private static Debt Friend(string id = "friend", RepaymentPlan? plan = null, string? iban = FriendIban, TxnId? received = null, string lender = "Friend One", DateOnly? borrowedOn = null) =>
		Debt.Create(new DebtId(id), new DebtTerms(DebtKind.PersonalLoan, lender, iban, received, 1000m, null, plan, borrowedOn ?? new DateOnly(2026, 10, 1))).Value;

	/// <summary>
	/// Creates a bank loan with the given series key, or any loan series when it is <see langword="null"/>.
	/// </summary>
	private static Debt Bank(string? seriesKey, RepaymentPlan? plan = null) =>
		Debt.Create(new DebtId("bank"), new DebtTerms(DebtKind.BankLoan, "KBC", null, null, 1500m, seriesKey, plan, new DateOnly(2026, 10, 1))).Value;

	/// <summary>
	/// Runs the analysis over the payments with the given debts.
	/// </summary>
	private static Analysis Analyse(IEnumerable<Txn> txns, params Debt[] debts) =>
		new([.. txns], CategoryDefaults.CreateDefaults("BE"), UserData.Rehydrate([], [], [], [], [], [], debts), Today);

	/// <summary>
	/// A transfer to the invented friend.
	/// </summary>
	private static Txn ToFriend(string date, decimal amount, string name = "Friend One", string iban = FriendIban) => BankJson.TransferOut(date, amount, name, iban);
	#endregion

	#region Matching Tests
	/// <summary>
	/// Verifies that transfers to the lender's IBAN are matched, laid over the schedule and counted as paid, and that a
	/// debt on schedule is on track.
	/// </summary>
	[Fact]
	public void Match_TransfersToLenderIban_PaidAndScheduleCovered()
	{
		var status = Analyse([ToFriend("2026-10-05", 250m), ToFriend("2026-11-05", 250m)], Friend(plan: MonthlyPlan())).Debts.Single();

		status.Paid.Should().Be(500m);
		status.Remaining.Should().Be(500m);
		status.Payments.Should().HaveCount(2);
		status.Schedule.Select(r => r.IsPaid).Should().Equal(true, true, false, false);
		status.NextDueDate.Should().Be(new DateOnly(2026, 12, 5));
		status.NextDueAmount.Should().Be(250m);
		status.OverdueAmount.Should().Be(0m);
		status.State.Should().Be(DebtState.OnTrack);
		status.Progress.Should().Be(0.5m);
	}

	/// <summary>
	/// Verifies that without an IBAN the lender's normalised name matches, and that another name does not.
	/// </summary>
	[Fact]
	public void Match_NoIban_NormalisedNameMatches()
	{
		var analysis = Analyse(
			[ToFriend("2026-10-05", 250m, "FRIEND  one", "BE99000011112222"), ToFriend("2026-10-06", 100m, "Someone Else", "BE99000011113333")],
			Friend(plan: MonthlyPlan(), iban: null));

		analysis.Debts.Single().Payments.Select(p => p.T.Amount).Should().Equal(-250m);
	}

	/// <summary>
	/// Verifies that the IBAN wins over the name: a payment to the same name but another IBAN is no repayment.
	/// </summary>
	[Fact]
	public void Match_SameNameOtherIban_NotMatched()
	{
		var analysis = Analyse([ToFriend("2026-10-05", 250m, iban: "BE99000011112222")], Friend(plan: MonthlyPlan()));

		analysis.Debts.Single().Payments.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that payments dated before the day the money was borrowed are not taken.
	/// </summary>
	[Fact]
	public void Match_BeforeBorrowedOn_NotMatched()
	{
		var analysis = Analyse([ToFriend("2026-09-30", 250m), ToFriend("2026-10-05", 250m)], Friend(plan: MonthlyPlan()));

		analysis.Debts.Single().Payments.Select(p => p.T.Date).Should().Equal(new DateOnly(2026, 10, 5));
	}

	/// <summary>
	/// Verifies that payments count from the borrowed-on day, not from the plan's first date.
	/// </summary>
	[Fact]
	public void Match_BorrowedOnPayoutDate_PaymentsFromThatDayCount()
	{
		var payout = BankJson.TransferIn("2026-09-01", 1000m, "Friend One", "Borrowed", debtorIban: FriendIban);
		var analysis = Analyse([payout, ToFriend("2026-08-31", 100m), ToFriend("2026-09-01", 100m), ToFriend("2026-09-15", 100m)],
			Friend(plan: MonthlyPlan(), received: payout.Id, borrowedOn: payout.Date));

		analysis.Debts.Single().Payments.Select(p => p.T.Date).Should().Equal(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15));
	}

	/// <summary>
	/// Verifies that oldest payments are taken first and matching stops once the amount to repay is covered; a later
	/// payment stays an ordinary payment.
	/// </summary>
	[Fact]
	public void Match_CoverageReached_LaterPaymentsNotTaken()
	{
		var plan = RepaymentPlan.OnePayment(500m, new DateOnly(2026, 10, 1)).Value;
		var analysis = Analyse([ToFriend("2026-10-05", 250m), ToFriend("2026-10-20", 250m), ToFriend("2026-11-05", 250m)], Friend(plan: plan));

		var status = analysis.Debts.Single();
		status.Payments.Select(p => p.T.Date).Should().Equal(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 20));
		status.State.Should().Be(DebtState.Repaid);
		status.Remaining.Should().Be(0m);
		analysis.Items.Single(i => i.T.Date == new DateOnly(2026, 11, 5)).Repayment.Should().BeFalse();
	}

	/// <summary>
	/// Verifies that a payment counts for one debt only: the second debt takes the next payment.
	/// </summary>
	[Fact]
	public void Match_TwoDebtsSameLender_EachPaymentCountsOnce()
	{
		var plan = RepaymentPlan.OnePayment(250m, new DateOnly(2026, 10, 1)).Value;
		var analysis = Analyse([ToFriend("2026-10-05", 250m), ToFriend("2026-10-20", 250m)], Friend("first", plan), Friend("second", plan));

		analysis.Debts[0].Payments.Select(p => p.T.Date).Should().Equal(new DateOnly(2026, 10, 5));
		analysis.Debts[1].Payments.Select(p => p.T.Date).Should().Equal(new DateOnly(2026, 10, 20));
	}

	/// <summary>
	/// Verifies that a payment the owner added to one debt is never taken automatically by another.
	/// </summary>
	[Fact]
	public void Match_AddedByHandToAnotherDebt_NotTakenAutomatically()
	{
		var plan = RepaymentPlan.OnePayment(250m, new DateOnly(2026, 10, 1)).Value;
		var oldest = ToFriend("2026-10-05", 250m);
		var next = ToFriend("2026-10-20", 250m);
		var second = Friend("second", plan);
		second.Include(oldest.Id);

		var analysis = Analyse([oldest, next], Friend("first", plan), second);

		analysis.Debts[0].Payments.Select(p => p.T.Id).Should().Equal(next.Id);
		analysis.Debts[1].Payments.Select(p => p.T.Id).Should().Equal(oldest.Id);
	}

	/// <summary>
	/// Verifies that a payment the owner added always counts, even to another IBAN and before the start, and counts first.
	/// </summary>
	[Fact]
	public void Match_AddedByHand_AlwaysCounts()
	{
		var cash = ToFriend("2026-01-01", 400m, "Somebody", "BE99000011112222");
		var debt = Friend(plan: MonthlyPlan());
		debt.Include(cash.Id);

		var status = Analyse([cash, ToFriend("2026-10-05", 250m)], debt).Debts.Single();

		status.Payments.Select(p => p.T.Amount).Should().Equal(-400m, -250m);
		status.Paid.Should().Be(650m);
	}

	/// <summary>
	/// Verifies that an excluded payment never counts, and the next oldest one is taken instead.
	/// </summary>
	[Fact]
	public void Match_ExcludedByHand_NeverCounts()
	{
		var first = ToFriend("2026-10-05", 250m);
		var second = ToFriend("2026-10-20", 250m);
		var debt = Friend(plan: RepaymentPlan.OnePayment(250m, new DateOnly(2026, 10, 1)).Value);
		debt.Exclude(first.Id);

		var analysis = Analyse([first, second], debt);

		analysis.Debts.Single().Payments.Select(p => p.T.Id).Should().Equal(second.Id);
		analysis.Items.Single(i => i.T.Id == first.Id).Repayment.Should().BeFalse();
	}

	/// <summary>
	/// Verifies that a bank loan with a series key takes only that series, and without one takes any loan instalment.
	/// </summary>
	[Fact]
	public void Match_BankLoan_BySeriesOrAnyLoanInstalment()
	{
		var mine = BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01");
		var other = BankJson.LoanInstalment("2026-10-08", 100m, "999-0000000-09");

		Analyse([mine, other], Bank("LOAN:726-0000000-01")).Debts.Single().Payments.Select(p => p.T.Id).Should().Equal(mine.Id);
		Analyse([mine, other], Bank(null)).Debts.Single().Payments.Select(p => p.T.Id).Should().Equal(mine.Id, other.Id);
	}

	/// <summary>
	/// Verifies that a transfer to someone else is never taken as the repayment of a bank loan.
	/// </summary>
	[Fact]
	public void Match_BankLoan_IgnoresTransfers() =>
		Analyse([ToFriend("2026-10-05", 250m)], Bank(null)).Debts.Single().Payments.Should().BeEmpty();
	#endregion

	#region Status Tests
	/// <summary>
	/// Verifies that without a plan the debt has the state "no plan" and the whole amount remains.
	/// </summary>
	[Fact]
	public void Status_NoPlan_NoPlanState()
	{
		var status = Analyse([], Friend()).Debts.Single();

		status.State.Should().Be(DebtState.NoPlan);
		status.Remaining.Should().Be(1000m);
		status.Schedule.Should().BeEmpty();
		status.NextDueDate.Should().BeNull();
	}

	/// <summary>
	/// Verifies that a missed payment makes the debt overdue, with the overdue amount and the oldest missed date.
	/// </summary>
	[Fact]
	public void Status_MissedPayments_OverdueWithAmountAndOldestDate()
	{
		var status = Analyse([], Friend(plan: MonthlyPlan())).Debts.Single();

		status.State.Should().Be(DebtState.Overdue);
		status.OverdueAmount.Should().Be(500m);
		status.OldestMissedDate.Should().Be(new DateOnly(2026, 10, 5));
		status.NextDueDate.Should().Be(new DateOnly(2026, 12, 5));
	}

	/// <summary>
	/// Verifies that a partly paid payment counts as overdue for the rest only.
	/// </summary>
	[Fact]
	public void Status_PartlyPaid_OverdueForTheRest()
	{
		var status = Analyse([ToFriend("2026-10-06", 100m)], Friend(plan: MonthlyPlan())).Debts.Single();

		status.Schedule[0].Covered.Should().Be(100m);
		status.OverdueAmount.Should().Be(400m);
		status.OldestMissedDate.Should().Be(new DateOnly(2026, 10, 5));
	}

	/// <summary>
	/// Verifies that a payment due within seven days, the seventh day and today included, is due soon, and one due in
	/// eight days is not.
	/// </summary>
	/// <param name="due">The due date of the only payment.</param>
	/// <param name="expected">The expected state.</param>
	[Theory]
	[InlineData("2026-11-09", DebtState.DueSoon)]
	[InlineData("2026-11-16", DebtState.DueSoon)]
	[InlineData("2026-11-17", DebtState.OnTrack)]
	public void Status_NextPayment_DueSoonWithinSevenDays(string due, DebtState expected)
	{
		var plan = RepaymentPlan.OnePayment(100m, DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture)).Value;

		var status = Analyse([], Friend(plan: plan)).Debts.Single();

		status.State.Should().Be(expected);
		status.OverdueAmount.Should().Be(0m);
	}

	/// <summary>
	/// Verifies that a payment due yesterday is overdue, while one due today is not.
	/// </summary>
	[Fact]
	public void Status_DueYesterday_Overdue()
	{
		var plan = RepaymentPlan.OnePayment(100m, new DateOnly(2026, 11, 8)).Value;

		Analyse([], Friend(plan: plan)).Debts.Single().State.Should().Be(DebtState.Overdue);
	}

	/// <summary>
	/// Verifies that exactly covering the amount to repay is repaid and covering more is overpaid.
	/// </summary>
	[Fact]
	public void Status_ExactlyCoveredRepaid_MoreThanCoveredOverpaid()
	{
		var plan = RepaymentPlan.OnePayment(500m, new DateOnly(2026, 10, 1)).Value;
		var exact = Analyse([ToFriend("2026-10-05", 500m)], Friend(plan: plan)).Debts.Single();
		var extra = Friend(plan: plan);
		var big = ToFriend("2026-10-05", 650m);
		extra.Include(big.Id);
		var over = Analyse([big], extra).Debts.Single();

		exact.State.Should().Be(DebtState.Repaid);
		over.State.Should().Be(DebtState.Overpaid);
		over.Overpaid.Should().Be(150m);
		over.Remaining.Should().Be(0m);
	}

	/// <summary>
	/// Verifies that the last instalment, smaller than the others, is what completes the schedule.
	/// </summary>
	[Fact]
	public void Status_LastInstalmentSmaller_ScheduleCompletes()
	{
		var plan = RepaymentPlan.Instalments(1000m, new DateOnly(2026, 10, 5), RepaymentFrequency.Monthly, null, 300m, null).Value;
		var payments = new[] { ToFriend("2026-10-05", 300m), ToFriend("2026-11-05", 300m), ToFriend("2026-12-05", 300m), ToFriend("2027-01-05", 100m) };

		var status = Analyse(payments, Friend(plan: plan)).Debts.Single();

		status.State.Should().Be(DebtState.Repaid);
		status.Schedule.Should().OnlyContain(r => r.IsPaid);
	}
	#endregion

	#region Loan Credit Tests
	/// <summary>
	/// Verifies that a loan payout no debt tracks is listed as unplanned, and one a debt tracks is not.
	/// </summary>
	[Fact]
	public void Unplanned_LoanPayoutWithoutDebt_Listed()
	{
		var payout = BankJson.TransferIn("2026-10-05", 10000m, "KBC BANK NV", "Loan payout", subCode: "CAJT");
		var other = BankJson.TransferIn("2026-10-06", 20m, "Employer", "Salary");

		Analyse([payout, other]).UnplannedLoans.Select(i => i.T.Id).Should().Equal(payout.Id);

		var tracked = Bank("LOAN:1");
		tracked.Revise(new DebtTerms(DebtKind.BankLoan, "KBC", null, payout.Id, 10000m, "LOAN:1", null, new DateOnly(2026, 10, 5)));
		Analyse([payout, other], tracked).UnplannedLoans.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that a credit the owner put under Borrowed from people counts as a loan credit and is not income.
	/// </summary>
	[Fact]
	public void Unplanned_BorrowedFromPeople_ListedAndNotIncome()
	{
		var borrowed = BankJson.TransferIn("2026-10-05", 300m, "Friend One", "Thanks", debtorIban: FriendIban);
		var user = UserData.Empty();
		user.AssignTransactionCategory(borrowed.Id, CategoryId.BorrowedPeople);

		var analysis = new Analysis([borrowed], CategoryDefaults.CreateDefaults("BE"), user, Today);

		analysis.UnplannedLoans.Should().ContainSingle();
		analysis.MonthData("2026-10").Income.Should().Be(0m);
		analysis.MonthData("2026-10").LoanReceived.Should().Be(300m);
	}
	#endregion
}
