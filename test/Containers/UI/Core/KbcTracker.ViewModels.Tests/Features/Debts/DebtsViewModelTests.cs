namespace KbcTracker.ViewModels.Tests.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="DebtsViewModel"/>: the order of the cards (active first, the most urgent first), the words
///              of each status pill, the schedule lines, the payments counted with their "Not a repayment" requests, the
///              payments that can still be added, the unplanned credits and the plan forms it opens.
/// </summary>
public sealed class DebtsViewModelTests
{
	#region Test Infrastructure
	/// <summary>Today in every analysis: 9 November 2026.</summary>
	private static DateOnly Today => TestWorkspace.Today;

	/// <summary>Builds a debt view model.</summary>
	private static DebtsViewModel Build(Analysis analysis) => new(analysis, Formats.Belgian, Today);

	/// <summary>One payment of 100 due on a date.</summary>
	private static RepaymentPlan OneOf(decimal amount, DateOnly due) => RepaymentPlan.OnePayment(amount, due).Value;
	#endregion

	#region Card Tests
	/// <summary>Verifies the overdue debt comes first, then the one on track, the repaid one last, whatever the order they were added in.</summary>
	[Fact]
	public void Cards_ActiveFirstMostUrgentFirst_RepaidLast()
	{
		var analysis = DebtTestData.Analyse(
			[DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 5), 100m), DebtTestData.ToFriend("p1", new DateOnly(2026, 10, 20), 100m)],
			("repaid", DebtTestData.FriendTerms(OneOf(100m, new DateOnly(2026, 10, 15)), lender: "Alpha")),
			("later", DebtTestData.FriendTerms(OneOf(100m, new DateOnly(2026, 12, 20)), creditId: null, lender: "Gamma")),
			("late", DebtTestData.FriendTerms(OneOf(100m, new DateOnly(2026, 10, 1)), creditId: null, lender: "Beta")));

		var cards = Build(analysis).Cards;

		cards.Select(c => c.Lender).Should().Equal("Beta", "Gamma", "Alpha");
		cards.Select(c => c.IsActive).Should().Equal(true, true, false);
	}

	/// <summary>Verifies the pill words: overdue with the amount, due soon, on track, repaid and no plan yet.</summary>
	[Fact]
	public void Pill_EachState_Words()
	{
		var analysis = DebtTestData.Analyse(
			[DebtTestData.ToFriend("p1", new DateOnly(2026, 10, 20), 100m)],
			("done", DebtTestData.FriendTerms(OneOf(100m, new DateOnly(2026, 10, 15)), creditId: null, lender: "Friend One")),
			("late", DebtTestData.FriendTerms(OneOf(300m, new DateOnly(2026, 10, 1)), creditId: null, lender: "A late")),
			("soon", DebtTestData.FriendTerms(OneOf(50m, new DateOnly(2026, 11, 12)), creditId: null, lender: "B soon")),
			("track", DebtTestData.FriendTerms(OneOf(50m, new DateOnly(2026, 12, 12)), creditId: null, lender: "C track")),
			("none", DebtTestData.FriendTerms(null, creditId: null, lender: "D none")));

		var pills = Build(analysis).Cards.ToDictionary(c => c.Lender, c => c.Pill, StringComparer.Ordinal);

		pills["A late"].Text.Should().StartWith("Overdue ").And.Contain(Formats.Belgian.Money(300m));
		pills["A late"].Tone.Should().Be(PillTone.Bad);
		pills["B soon"].Text.Should().Be("Due soon");
		pills["C track"].Text.Should().Be("On track");
		pills["D none"].Text.Should().Be("No plan yet");
		pills["Friend One"].Text.Should().Be("Repaid");
	}

	/// <summary>Verifies the card of a part-repaid debt: progress, what remains, the next payment, the plan and the schedule states.</summary>
	[Fact]
	public void Card_PartRepaid_ProgressNextAndSchedule()
	{
		var plan = DebtTestData.Monthly(1000m, new DateOnly(2026, 10, 5), 250m);
		var analysis = DebtTestData.Analyse(
			[DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 1000m), DebtTestData.ToFriend("p1", new DateOnly(2026, 10, 5), 250m), DebtTestData.ToFriend("p2", new DateOnly(2026, 11, 6), 100m)],
			("d1", DebtTestData.FriendTerms(plan, principal: 1000m, borrowedOn: new DateOnly(2026, 10, 1))));

		var card = Build(analysis).Cards.Single();

		card.ProgressPercent.Should().Be(35);
		card.ProgressText.Should().Be($"{Formats.Belgian.Money(350m)} of {Formats.Belgian.Money(1000m)}");
		card.RemainingText.Should().Be(Formats.Belgian.Money(650m));
		card.NextText.Should().Be($"{Formats.Belgian.Money(250m)} on 05/12/26");
		card.PlanText.Should().Contain("4 ×");
		card.Schedule.Select(l => l.StateText).Should().Equal("Paid", $"Overdue, Partly paid, {Formats.Belgian.Money(150m)} left", "Upcoming", "Upcoming");
		card.Payments.Select(p => p.Id).Should().Equal("p1", "p2");
		card.Payments[0].NotARepaymentRequest.Should().Be(DecisionRequests.UnmatchRepayment("d1", "p1"));
		card.RemoveRequest.Should().Be(DecisionRequests.RemoveDebt("d1"));
	}

	/// <summary>Verifies the payments to add leave out the ones a debt counts already and the money in.</summary>
	[Fact]
	public void Addable_LeavesOutCountedPaymentsAndCredits()
	{
		var analysis = DebtTestData.Analyse(
			[DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 100m), DebtTestData.ToFriend("p1", new DateOnly(2026, 10, 5), 100m), TestWorkspace.Payment("o1", new DateOnly(2026, 10, 6), -9m, "Kiosk")],
			("d1", DebtTestData.FriendTerms(OneOf(100m, new DateOnly(2026, 10, 5)), principal: 100m, borrowedOn: new DateOnly(2026, 10, 1))));

		var card = Build(analysis).Cards.Single();

		card.Addable.Select(a => a.Id).Should().Equal("o1");
		card.AddPaymentRequest("o1").Should().Be(DecisionRequests.MatchRepayment("d1", "o1"));
	}
	#endregion

	#region Unplanned Tests
	/// <summary>Verifies a credit without a debt is listed and opens a form, and that it is not listed once a debt tracks it.</summary>
	[Fact]
	public void Unplanned_CreditWithoutDebt_ListedAndPlannable()
	{
		var credit = DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 5), 300m);

		var view = Build(DebtTestData.Analyse([credit]));

		view.Unplanned.Should().ContainSingle().Which.Payer.Should().Be(DebtTestData.Friend);
		view.PlanForCredit("c1").Should().NotBeNull();
		view.PlanForCredit("missing").Should().BeNull();
		Build(DebtTestData.Analyse([credit], ("d1", DebtTestData.FriendTerms(null)))).Unplanned.Should().BeEmpty();
	}

	/// <summary>Verifies an empty workspace has no card and no unplanned credit, and the forms open for a debt and a new loan.</summary>
	[Fact]
	public void Forms_ExistingDebtAndNewDebt_Open()
	{
		var view = Build(DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 5), 300m)], ("d1", DebtTestData.FriendTerms(null))));

		view.PlanForDebt("d1")!.DebtId.Should().Be("d1");
		view.PlanForDebt("zzz").Should().BeNull();
		view.PlanForNewDebt().IsNew.Should().BeTrue();
		Build(TestWorkspace.Analyse([])).Cards.Should().BeEmpty();
	}
	#endregion
}
