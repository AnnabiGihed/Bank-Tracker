using System.Net;

namespace KbcTracker.Components.Tests.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="DebtsPage"/> sends the right decisions: a debt's card with its status, the expandable
///              details with "Not a repayment" and "Add a payment", "Edit plan", "Remove" after a confirmation, "Plan it" for an
///              unplanned credit, and "Add debt" for a loan that has no credit.
/// </summary>
public sealed class DebtsPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds a loan of 1000 from the friend, repaid by instalments of 250 with one payment made, and an unrelated debit.</summary>
	private static Analysis WithDebt() => DebtTestData.Analyse(
		[
			DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 1000m),
			DebtTestData.ToFriend("p1", new DateOnly(2026, 10, 5), 250m),
			TestWorkspace.Payment("o1", new DateOnly(2026, 10, 6), -9m, "Kiosk"),
		],
		("d1", DebtTestData.FriendTerms(DebtTestData.Monthly(1000m, new DateOnly(2026, 10, 5), 250m), principal: 1000m, borrowedOn: new DateOnly(2026, 10, 1))));

	/// <summary>Makes the backend accept decisions.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, "{}");

	/// <summary>Renders the page with the first card's details open.</summary>
	private async Task<IRenderedComponent<DebtsPage>> RenderOpenAsync()
	{
		Serve(WithDebt());
		AcceptDecisions();
		var page = Render<DebtsPage>();
		await page.WaitForAssertionAsync(() => page.Find("#debt-d1")).ConfigureAwait(true);
		await ButtonWithText(page, "Details").ClickAsync().ConfigureAwait(true);
		return page;
	}
	#endregion

	#region Content Tests
	/// <summary>Verifies the card shows the lender, kind, progress and what remains, and the details list the schedule and the counted payment.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Debt_ShowsCardAndDetails()
	{
		var page = await RenderOpenAsync();

		var card = page.Find("#debt-d1").TextContent;
		card.Should().Contain(DebtTestData.Friend).And.Contain("Personal loan").And.Contain(Formats.Belgian.Money(750m));
		page.Find("#debt-d1 [role='progressbar']").GetAttribute("aria-valuenow").Should().Be("25");
		page.Find("#debt-d1 table").TextContent.Should().Contain("Paid").And.Contain(Formats.Belgian.Money(250m));
		page.Find("#debt-d1 ul.payments").TextContent.Should().Contain(DebtTestData.Friend);
	}

	/// <summary>Verifies the empty state says how to get a debt.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NoDebts_ShowsTheEmptyMessage()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -5m, "Kiosk")]));

		var page = Render<DebtsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain(DebtsViewModel.EmptyMessage));
	}
	#endregion

	#region Decision Tests
	/// <summary>Verifies "Not a repayment" sends unmatchRepayment for the debt and the payment.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task NotARepayment_Clicked_SendsUnmatchRepayment()
	{
		var page = await RenderOpenAsync();

		await page.Find("button[aria-label*='is not a repayment of']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"unmatchRepayment\"").And.Contain("\"d1\"").And.Contain("\"p1\""));
	}

	/// <summary>Verifies picking a payment in "Add a payment" sends matchRepayment, and the counted payment is not on offer.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task AddPayment_Picked_SendsMatchRepayment()
	{
		var page = await RenderOpenAsync();
		var picker = page.FindComponent<RadzenDropDown<string>>();
		(picker.Instance.Data as IEnumerable<PaymentOption> ?? []).Select(o => o.Id).Should().Equal("o1");

		await page.InvokeAsync(() => picker.Instance.ValueChanged.InvokeAsync("o1"));

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"matchRepayment\"").And.Contain("\"d1\"").And.Contain("\"o1\""));
	}

	/// <summary>Verifies Remove asks first and then sends removeDebt.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Remove_Confirmed_SendsRemoveDebt()
	{
		Serve(WithDebt());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<DebtsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label^='Remove ']"));

		var removing = page.Find("button[aria-label^='Remove ']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain(DebtsViewModel.RemovePrompt));
		Backend.LastDecisionBody.Should().BeNull();
		await ButtonWithText(host, "Remove").ClickAsync();
		await removing;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"removeDebt\"").And.Contain("\"d1\""));
	}

	/// <summary>Verifies Edit plan opens the form on the stored plan and Save sends updateDebt for that debt.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task EditPlan_Saved_SendsUpdateDebt()
	{
		Serve(WithDebt());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<DebtsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label^='Edit plan of']"));

		var editing = page.Find("button[aria-label^='Edit plan of']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("[data-testid='plan-preview']"));
		await ButtonWithText(host, "Save").ClickAsync();
		await editing;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"updateDebt\"").And.Contain("\"d1\"").And.Contain("\"borrowedOn\""));
	}

	/// <summary>Verifies "Plan it" opens the form for an unplanned credit and Skip for now sends addDebt for that credit without a plan.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task PlanIt_Skipped_SendsAddDebtWithoutPlan()
	{
		Serve(DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 1), 300m)]));
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<DebtsPage>();
		await page.WaitForAssertionAsync(() => page.Find("#debts-unplanned").TextContent.Should().Contain(DebtTestData.Friend));

		var planning = ButtonWithText(page, "Plan it").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("How is this repaid?"));
		await ButtonWithText(host, "Skip for now").ClickAsync();
		await planning;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"addDebt\"").And.Contain("\"c1\"").And.NotContain("\"plan\":{"));
	}

	/// <summary>Verifies "Add debt" opens an empty form whose Save stays disabled until it is complete.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task AddDebt_Opened_EmptyFormCannotSaveYet()
	{
		Serve(TestWorkspace.Analyse([]));
		var host = RenderRadzenHost();
		var page = Render<DebtsPage>();
		await page.WaitForAssertionAsync(() => ButtonWithText(page, "Add debt"));

		var adding = ButtonWithText(page, "Add debt").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("Borrowed from"));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();
		await ButtonWithText(host, "Cancel").ClickAsync();
		await adding;
		Backend.LastDecisionBody.Should().BeNull();
	}
	#endregion
}
