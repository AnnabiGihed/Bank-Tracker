using System.Net;

namespace KbcTracker.Components.Tests.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="IncomePage"/>: the month's sections with their payments and totals, the earnings by
///              payer and the chart's numbers for screen readers, the confirm and category actions, and the empty state.
/// </summary>
public sealed class IncomePageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds a November with a salary, a refund and a loan payout.</summary>
	private static Analysis November() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), 30m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 4), 5000m, "Kbc Bank"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 11, 1), 2500m, "Acme Payroll", "Salary November"),
	], (user, _) =>
	{
		user.AssignTransactionCategory(new TxnId("t1"), new CategoryId("refunds"));
		user.AssignTransactionCategory(new TxnId("t2"), CategoryId.LoanReceived);
	});

	/// <summary>Changes the category drop-down of the page's first payment, as the owner does by picking an entry.</summary>
	private static async Task PickCategoryAsync(IRenderedComponent<IncomePage> page, string categoryId)
	{
		var dropDown = page.FindComponent<CategoryPicker>().FindComponent<RadzenDropDown<string>>();
		await page.InvokeAsync(() => dropDown.Instance.Change.InvokeAsync(categoryId)).ConfigureAwait(true);
	}
	#endregion

	#region Content Tests
	/// <summary>Verifies the three sections show their totals and payments, and the payer and chart numbers are there.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Month_ShowsSectionsPayersAndChartNumbers()
	{
		Serve(November());

		var page = Render<IncomePage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Acme Payroll"));
		page.Find("#income-earnings").TextContent.Should().Contain(Formats.Belgian.Money(2500m)).And.Contain("Salary");
		page.Find("#income-refunds").TextContent.Should().Contain("Shop One").And.Contain(Formats.Belgian.Money(30m));
		page.Find("#income-loan-received").TextContent.Should().Contain("Kbc Bank").And.Contain(Formats.Belgian.Money(5000m));
		page.Markup.Should().Contain("Earnings by payer");
		page.Find("table.sr-only").TextContent.Should().Contain(Formats.Belgian.Money(2500m));
	}

	/// <summary>Verifies a month without money in says so in each section.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NoMoneyIn_ShowsEmptyMessages()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -5m, "Kiosk")]));

		var page = Render<IncomePage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("No earnings this month."));
		page.Markup.Should().Contain("No refunds this month.").And.Contain("No loan money received this month.").And.Contain("No earnings yet.");
	}

	/// <summary>Verifies the <c>show</c> query is accepted and the page still renders every section.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_ShowInAddress_RendersTheSections()
	{
		Serve(November());
		Services.GetRequiredService<NavigationManager>().NavigateTo("/income?m=2026-11&show=refunds");

		var page = Render<IncomePage>();

		await page.WaitForAssertionAsync(() => page.FindAll("h2[tabindex='-1']").Should().HaveCount(3));
	}
	#endregion

	#region Decision Tests
	/// <summary>Verifies Confirm sends the confirm decision of the payer.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Confirm_Payment_SendsConfirm()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t3", new DateOnly(2026, 11, 1), 2500m, "Acme Payroll", "Salary November")]));
		Backend.Respond("/api/v1/decisions", HttpStatusCode.OK, "{}");
		var page = Render<IncomePage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Confirm the category of Acme Payroll']"));

		await page.Find("button[aria-label='Confirm the category of Acme Payroll']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"confirm\""));
	}

	/// <summary>Verifies picking a category sets it for the payer and offers "Only this one", which sends the txnOnly decision with the restore data.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ChangeCategory_Picked_OffersOnlyThisOne()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t3", new DateOnly(2026, 11, 1), 2500m, "Acme Payroll", "Salary November")]));
		Backend.Respond("/api/v1/decisions", HttpStatusCode.OK, """{"previousAssignment":{"categoryId":"income-salary","confirmed":false}}""");
		var host = RenderRadzenHost();
		var page = Render<IncomePage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Acme Payroll"));

		await PickCategoryAsync(page, "income-other");

		await host.WaitForAssertionAsync(() => host.Find("[data-testid='undo-category']"));
		Backend.LastDecisionBody.Should().Contain("\"payeeCategory\"").And.Contain("\"income-other\"");
		host.Markup.Should().Contain("set for all payments to Acme Payroll");

		await host.Find("[data-testid='undo-category']").ClickAsync();

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"txnOnly\""));
		Backend.LastDecisionBody.Should().Contain("\"t3\"").And.Contain("\"income-other\"").And.Contain("\"restore\"");
	}
	#endregion
}
