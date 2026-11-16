namespace KbcTracker.ViewModels.Tests.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="DebtNotice"/> and its place in the Overview's <see cref="AttentionNotice"/>: the words for
///              a payment due soon, overdue repayments and loan credits without a plan, and no notice when there is nothing.
/// </summary>
public sealed class DebtNoticeTests
{
	#region Test Infrastructure
	/// <summary>Today in every analysis: 9 November 2026.</summary>
	private static DateOnly Today => TestWorkspace.Today;
	#endregion

	#region Notice Tests
	/// <summary>Verifies a payment due in three days, an overdue repayment and unplanned credits are worded, in that order.</summary>
	[Fact]
	public void From_DueOverdueAndUnplanned_WordedInOrder()
	{
		var analysis = DebtTestData.Analyse(
			[DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 300m), DebtTestData.FriendCredit("c2", new DateOnly(2026, 10, 2), 40m), DebtTestData.FriendCredit("c3", new DateOnly(2026, 10, 3), 50m)],
			("soon", DebtTestData.FriendTerms(RepaymentPlan.OnePayment(250m, new DateOnly(2026, 11, 12)).Value, creditId: "c1", lender: "Jan")),
			("late", DebtTestData.FriendTerms(RepaymentPlan.OnePayment(100m, new DateOnly(2026, 11, 1)).Value, creditId: null, lender: "Pia")));

		var notice = DebtNotice.From(analysis, Formats.Belgian, Today)!;

		notice.Lines.Should().Equal($"{Formats.Belgian.Money(250m)} to Jan due in 3 days", "1 repayment overdue", "2 loan credits without a plan");
	}

	/// <summary>Verifies a payment due today or tomorrow is worded as such, and a lone credit uses the singular.</summary>
	[Fact]
	public void From_DueTodayAndOneCredit_Singular()
	{
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 300m)],
			("today", DebtTestData.FriendTerms(RepaymentPlan.OnePayment(10m, Today).Value, creditId: null, lender: "Jan")));

		DebtNotice.From(analysis, Formats.Belgian, Today)!.Lines.Should().Equal($"{Formats.Belgian.Money(10m)} to Jan due today", "1 loan credit without a plan");
	}

	/// <summary>Verifies nothing is said when there is no debt and no credit, and the Overview notice then does not exist.</summary>
	[Fact]
	public void From_Nothing_NullAndNoAttention()
	{
		var analysis = TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -5m, "Kiosk")]);

		DebtNotice.From(analysis, Formats.Belgian, Today).Should().BeNull();
		new OverviewViewModel(analysis, "2026-11", null, Formats.Belgian, Today).Attention?.Debts.Should().BeNull();
	}

	/// <summary>Verifies debts alone make the Overview show its notice, with the lines carried in it.</summary>
	[Fact]
	public void Overview_DebtsOnly_AttentionCarriesTheLines()
	{
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 11, 1), 300m), TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -5m, "Kiosk")]);

		var attention = new OverviewViewModel(analysis, "2026-11", null, Formats.Belgian, Today).Attention;

		attention.Should().NotBeNull();
		attention.Debts!.Lines.Should().Equal("1 loan credit without a plan");
	}
	#endregion
}
