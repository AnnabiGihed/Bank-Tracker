namespace KbcTracker.ViewModels.Tests.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="RepaymentPlanEdit"/>: what a loan credit prefills, the live preview of the plan, the
///              day of the month, the problems the form reports with the domain's words, and the requests it builds.
/// </summary>
public sealed class RepaymentPlanEditTests
{
	#region Test Infrastructure
	/// <summary>The invented day the friend lent the money.</summary>
	private static readonly DateOnly Lent = new(2026, 10, 5);

	/// <summary>Builds the form for a credit of the given amount from the friend, put under Borrowed from people.</summary>
	private static RepaymentPlanEdit ForFriendCredit(decimal amount = 1000m)
	{
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", Lent, amount)]);
		return RepaymentPlanEdit.ForCredit(analysis, analysis.UnplannedLoans.Single(), Formats.Belgian);
	}
	#endregion

	#region Prefill Tests
	/// <summary>Verifies a credit prefills the lender, IBAN, day borrowed, amount and a monthly plan a month later.</summary>
	[Fact]
	public void ForCredit_Friend_PrefilledFromThePayerAndPayment()
	{
		var edit = ForFriendCredit();

		edit.Kind.Should().Be(DebtKind.PersonalLoan);
		edit.Lender.Should().Be(DebtTestData.Friend);
		edit.Iban.Should().Be(DebtTestData.FriendIban);
		edit.BorrowedOn.Should().Be(Lent);
		edit.AmountToRepay.Should().Be(1000m);
		edit.ReceivedPaymentId.Should().Be("c1");
		edit.IsNew.Should().BeTrue();
		edit.Shape.Should().Be(RepaymentShape.Instalments);
		edit.Frequency.Should().Be(RepaymentFrequency.Monthly);
		edit.PaymentDate.Should().Be(new DateOnly(2026, 11, 5));
		edit.DayOfMonth.Should().Be(5);
	}

	/// <summary>Verifies a credit under Loan received itself presets a bank loan and links the one loan series paid after it.</summary>
	[Fact]
	public void ForCredit_LoanReceived_BankLoanWithTheOnlySeriesAfterThePayout()
	{
		var payout = DebtTestData.FriendCredit("c1", Lent, 5000m);
		var analysis = TestWorkspace.Analyse(
			[payout, DebtTestData.LoanInstalment("l1", new DateOnly(2026, 10, 20), 100m, "726-1"), DebtTestData.LoanInstalment("l0", new DateOnly(2026, 9, 20), 80m, "999-9")],
			(user, _) => user.AssignTransactionCategory(payout.Id, CategoryId.LoanReceived));

		var edit = RepaymentPlanEdit.ForCredit(analysis, analysis.UnplannedLoans.Single(), Formats.Belgian);

		edit.Kind.Should().Be(DebtKind.BankLoan);
		edit.ShowsSeries.Should().BeTrue();
		edit.SeriesChoices.Select(c => c.Key).Should().BeEquivalentTo("LOAN:726-1", "LOAN:999-9");
		edit.SeriesKey.Should().Be("LOAN:726-1");
		edit.AmountHint.Should().Contain("interest");
	}

	/// <summary>Verifies a form for a loan without a credit starts empty, for a person.</summary>
	[Fact]
	public void ForNewDebt_StartsEmpty_NoCreditNoSkipWithoutAmount()
	{
		var edit = RepaymentPlanEdit.ForNewDebt(TestWorkspace.Analyse([]), Formats.Belgian);

		edit.IsNew.Should().BeTrue();
		edit.ReceivedPaymentId.Should().BeNull();
		edit.Lender.Should().BeEmpty();
		edit.CanSkip.Should().BeFalse();
		edit.Problems.Should().NotBeEmpty();
	}

	/// <summary>Verifies an existing debt fills the form with what is stored.</summary>
	[Fact]
	public void ForDebt_Stored_FillsTheForm()
	{
		var plan = DebtTestData.Monthly(1000m, new DateOnly(2026, 11, 5), 250m);
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", Lent, 1000m)], ("d1", DebtTestData.FriendTerms(plan, principal: 1000m)));

		var edit = RepaymentPlanEdit.ForDebt(analysis, analysis.Debts.Single().Debt, Formats.Belgian);

		edit.IsNew.Should().BeFalse();
		edit.DebtId.Should().Be("d1");
		edit.InstalmentAmount.Should().Be(250m);
		edit.Count.Should().Be(4);
		edit.PaymentDate.Should().Be(new DateOnly(2026, 11, 5));
		edit.ToRequest().Action.Should().Be("updateDebt");
	}
	#endregion

	#region Preview Tests
	/// <summary>Verifies the preview words the count, the amount, the smaller last payment and the dates.</summary>
	[Fact]
	public void Preview_AmountEach_WordsCountLastAndRange()
	{
		var edit = ForFriendCredit(2820m);
		edit.PaymentDate = new DateOnly(2026, 12, 5);
		edit.InstalmentAmount = 250m;

		edit.PreviewText.Should().Be($"12 × {Formats.Belgian.Money(250m)}, last {Formats.Belgian.Money(70m)}, 5 Dec 2026 – 5 Nov 2027");
	}

	/// <summary>Verifies giving the number of payments derives the amount of each one.</summary>
	[Fact]
	public void Preview_Count_DerivesTheInstalment()
	{
		var edit = ForFriendCredit();
		edit.Input = InstalmentInput.Count;
		edit.Count = 4;

		edit.PreviewText.Should().Be($"4 × {Formats.Belgian.Money(250m)}, 5 Nov 2026 – 5 Feb 2027");
	}

	/// <summary>Verifies one payment is worded with its due date, and that the preview is empty while the plan is not valid.</summary>
	[Fact]
	public void Preview_OnePayment_DueDate_NoPreviewWhenInvalid()
	{
		var edit = ForFriendCredit();
		edit.Shape = RepaymentShape.OnePayment;

		edit.PreviewText.Should().Be($"{Formats.Belgian.Money(1000m)} on 5 Nov 2026");

		edit.PaymentDate = null;
		edit.PreviewText.Should().BeNull();
	}

	/// <summary>Verifies the day of the month is derived from the first date and hidden for weekly instalments and one payment.</summary>
	[Fact]
	public void DayOfMonth_FromTheFirstDate_HiddenForWeeklyAndOnePayment()
	{
		var edit = ForFriendCredit();
		edit.PaymentDate = new DateOnly(2026, 11, 17);

		edit.DayOfMonth.Should().Be(17);

		edit.Frequency = RepaymentFrequency.Weekly;
		edit.DayOfMonth.Should().BeNull();
		edit.ShowsDayOfMonth.Should().BeFalse();

		edit.Frequency = RepaymentFrequency.Yearly;
		edit.DayOfMonth.Should().Be(17);

		edit.Shape = RepaymentShape.OnePayment;
		edit.DayOfMonth.Should().BeNull();
	}
	#endregion

	#region Validation Tests
	/// <summary>Verifies a missing instalment amount, a missing count and a missing date are reported in words.</summary>
	[Fact]
	public void Problems_MissingFields_Reported()
	{
		var edit = ForFriendCredit();

		edit.IsValid.Should().BeFalse("no amount of each payment is given yet");
		edit.Problems.Should().Contain(RepaymentPlanEdit.EachAmountMissing);

		edit.Input = InstalmentInput.Count;
		edit.Problems.Should().Contain(RepaymentPlanEdit.CountMissing);

		edit.PaymentDate = null;
		edit.Problems.Should().Contain(RepaymentPlanEdit.FirstDateMissing);
		edit.Shape = RepaymentShape.OnePayment;
		edit.Problems.Should().Contain(RepaymentPlanEdit.DueDateMissing);
	}

	/// <summary>Verifies the domain's own words come through: an amount of each payment above the total, and a blank lender.</summary>
	[Fact]
	public void Problems_DomainRules_ReportedWithTheirWords()
	{
		var edit = ForFriendCredit();
		edit.InstalmentAmount = 5000m;
		edit.Lender = "  ";

		edit.Problems.Should().Contain(RepaymentPlanErrors.InstalmentAmountInvalid.Message).And.Contain(DebtErrors.LenderInvalid.Message);
		edit.CanSkip.Should().BeFalse();
		var save = () => edit.ToRequest();
		save.Should().Throw<InvalidOperationException>();
	}

	/// <summary>Verifies a form that is complete has no problem.</summary>
	[Fact]
	public void Problems_Complete_None()
	{
		var edit = ForFriendCredit();
		edit.InstalmentAmount = 250m;

		edit.Problems.Should().BeEmpty();
		edit.IsValid.Should().BeTrue();
	}
	#endregion

	#region Request Tests
	/// <summary>Verifies saving builds an addDebt request with the credit, the day borrowed and the instalment plan, without a day of month.</summary>
	[Fact]
	public void ToRequest_Instalments_AddDebtWithThePlan()
	{
		var edit = ForFriendCredit();
		edit.InstalmentAmount = 250m;

		var request = edit.ToRequest();

		request.Action.Should().Be("addDebt");
		request.Debt.Should().Be(new DebtRequest(DebtKind.PersonalLoan, DebtTestData.Friend, DebtTestData.FriendIban, "c1", null, null,
			new RepaymentPlanRequest(1000m, new DateOnly(2026, 11, 5), RepaymentFrequency.Monthly, null, 250m, null), Lent));
	}

	/// <summary>Verifies sizing by count sends the count and no instalment amount; one payment sends neither.</summary>
	[Fact]
	public void ToRequest_CountAndOnePayment_SendOnlyWhatApplies()
	{
		var edit = ForFriendCredit();
		edit.Input = InstalmentInput.Count;
		edit.Count = 4;
		edit.InstalmentAmount = 99m;

		edit.ToRequest().Debt!.Plan.Should().Be(new RepaymentPlanRequest(1000m, new DateOnly(2026, 11, 5), RepaymentFrequency.Monthly, null, null, 4));

		edit.Shape = RepaymentShape.OnePayment;
		edit.ToRequest().Debt!.Plan.Should().Be(new RepaymentPlanRequest(1000m, new DateOnly(2026, 11, 5)));
	}

	/// <summary>Verifies "Skip for now" saves the debt without a plan, even when the plan is not filled in.</summary>
	[Fact]
	public void ToSkipRequest_NoPlan_AddDebtWithoutPlan()
	{
		var edit = ForFriendCredit();

		edit.CanSkip.Should().BeTrue();
		var request = edit.ToSkipRequest();

		request.Action.Should().Be("addDebt");
		request.Debt!.Plan.Should().BeNull();
		request.Debt.ReceivedPaymentId.Should().Be("c1");
	}

	/// <summary>Verifies a loan without a credit sends the amount as what was received.</summary>
	[Fact]
	public void ToRequest_NoCredit_SendsThePrincipal()
	{
		var edit = RepaymentPlanEdit.ForNewDebt(TestWorkspace.Analyse([]), Formats.Belgian);
		edit.Lender = "Mum";
		edit.BorrowedOn = new DateOnly(2026, 5, 1);
		edit.AmountToRepay = 600m;
		edit.PaymentDate = new DateOnly(2026, 12, 1);
		edit.Shape = RepaymentShape.OnePayment;

		var debt = edit.ToRequest().Debt!;

		debt.ReceivedPaymentId.Should().BeNull();
		debt.Principal.Should().Be(600m);
		debt.BorrowedOn.Should().Be(new DateOnly(2026, 5, 1));
	}
	#endregion
}
