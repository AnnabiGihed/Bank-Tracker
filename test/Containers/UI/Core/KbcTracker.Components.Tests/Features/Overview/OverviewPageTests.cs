namespace KbcTracker.Components.Tests.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="OverviewPage"/>: the five key figures, the attention notice, the chart's numbers for
///              screen readers, the biggest expenses, the empty state, and the drill-down of the doughnut, whose leaf opens
///              Transactions filtered on that category.
/// </summary>
public sealed class OverviewPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds an analysis with a salary and a card payment in November.</summary>
	private static Analysis November() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -12.50m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 1), 2500m, "Acme Payroll", "Salary November"),
	]);
	#endregion

	#region Content Tests
	/// <summary>Verifies the key figures show the month's income and spending, formatted by the view model.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Month_ShowsTheFiveKeyFigures()
	{
		Serve(November());

		var page = Render<OverviewPage>();

		await page.WaitForAssertionAsync(() => page.Find("section[aria-label='Key figures']"));
		var figures = page.Find("section[aria-label='Key figures']").TextContent;
		figures.Should().Contain("Income").And.Contain(Formats.Belgian.Money(2500m));
		figures.Should().Contain("Spent").And.Contain(Formats.Belgian.Money(12.50m));
		figures.Should().Contain("Fixed payments").And.Contain("Saved & invested").And.Contain("Left over");
		page.FindAll("section[aria-label='Key figures'] .label").Should().HaveCount(5);
	}

	/// <summary>Verifies refunds and loan money show as their own figures, apart from the five key figures.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_RefundAndLoanMoney_ShowsSeparateFigures()
	{
		Serve(TestWorkspace.Analyse(
		[
			TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), 30m, "Shop One"),
			TestWorkspace.Payment("t2", new DateOnly(2026, 11, 4), 5000m, "Kbc Bank"),
		], (user, _) =>
		{
			user.AssignTransactionCategory(new TxnId("t1"), new CategoryId("refunds"));
			user.AssignTransactionCategory(new TxnId("t2"), CategoryId.LoanReceived);
		}));

		var page = Render<OverviewPage>();

		await page.WaitForAssertionAsync(() => page.Find("section[aria-label='Not counted in the totals']"));
		var figures = page.Find("section[aria-label='Not counted in the totals']").TextContent;
		figures.Should().Contain("Refunds").And.Contain(Formats.Belgian.Money(30m));
		figures.Should().Contain("Loan received").And.Contain(Formats.Belgian.Money(5000m));
		page.FindAll("section[aria-label='Key figures'] .label").Should().HaveCount(5);
	}

	/// <summary>Verifies the attention notice counts the payees whose automatic category waits for the owner.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_PayeesToReview_ShowsTheAttentionNotice()
	{
		var analysis = November();
		analysis.Review.Count.Should().BeGreaterThan(0);
		Serve(analysis);

		var page = Render<OverviewPage>();

		await page.WaitForAssertionAsync(() => page.Find("div.notice").TextContent.Should().Contain($"{analysis.Review.Count} payee(s) with an automatic category to confirm."));
		page.Find("div.notice a[href^='/review']").TextContent.Should().Contain("Review categories");
	}

	/// <summary>Verifies the biggest expenses list the payment with its date and amount, and the chart has a table of the same numbers.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Month_ListsBiggestExpensesAndChartNumbers()
	{
		Serve(November());

		var page = Render<OverviewPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Biggest expenses"));
		var expenses = page.FindAll("table").Single(t => t.TextContent.Contains("Shop One", StringComparison.Ordinal));
		expenses.TextContent.Should().Contain("03/11").And.Contain(Formats.Belgian.Money(12.50m));
		page.Find("figure table caption").TextContent.Should().Be("Last months in numbers");
		page.Find("figure tbody").TextContent.Should().Contain("Nov 26").And.Contain(Formats.Belgian.Money(2500m));
	}

	/// <summary>Verifies a workspace without transactions tells the owner what to do first.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NoTransactions_ShowsTheEmptyMessage()
	{
		Serve(TestWorkspace.Analyse([]), connected: false);

		var page = Render<OverviewPage>();

		await page.WaitForAssertionAsync(() => page.Find("p.empty").TextContent.Should().Contain("Connect your bank to get started"));
	}
	#endregion

	#region Drill-down Tests
	/// <summary>Verifies a slice without sub-categories opens Transactions filtered on its category, for the same month.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Slice_Leaf_OpensTransactionsOfThatCategory()
	{
		Serve(November());
		var page = Render<OverviewPage>();
		await page.WaitForAssertionAsync(() => page.FindAll("button.slice").Should().NotBeEmpty());

		await page.Find("button.slice").ClickAsync();

		Services.GetRequiredService<NavigationManager>().Uri.Should().Contain("/transactions?m=2026-11&cat=");
	}

	/// <summary>Verifies a slice that has spending below it drills down, the breadcrumb leads back, and the leaf below opens its transactions.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Slice_WithSubCategories_DrillsDownAndBack()
	{
		var analysis = TestWorkspace.Analyse(
			[TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -8m, "Rail Co")],
			(user, first) => user.ReassignPayee(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"), confirmed: true));
		Serve(analysis);
		var page = Render<OverviewPage>();
		await page.WaitForAssertionAsync(() => page.FindAll("button.slice").Should().ContainSingle());
		page.Find("button.slice").TextContent.Should().Contain("Trips");

		await page.Find("button.slice").ClickAsync();

		page.Find("nav[aria-label='Category path']").TextContent.Should().Contain("Trips");
		page.Find("button.slice").TextContent.Should().Contain("Rail trips").And.Contain(Formats.Belgian.Money(8m));
		page.Find("button.crumb").TextContent.Should().Be("All");

		await page.Find("button.crumb").ClickAsync();

		page.Find("button.slice").TextContent.Should().Contain("Trips");
	}
	#endregion
}
