using System.Net;

namespace KbcTracker.Components.Tests.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="TransactionsPage"/>: the rows of the month, the filters, the inline category change
///              that sets the payee's category and offers "Only this one" as an undo, confirm, mark as fixed, the category
///              filter taken from the address, and the split dialog opened from a row.
/// </summary>
public sealed class TransactionsPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds an analysis with a card payment out and a salary in, both in November.</summary>
	private static Analysis November(Action<UserData, Analysis>? decide = null) => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -12.50m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 1), 2500m, "Acme Payroll", "Salary November"),
	], decide);

	/// <summary>Makes the backend accept decisions and answer with the payee's previous assignment.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, """{"previousAssignment":{"categoryId":"other","confirmed":false}}""");

	/// <summary>Finds the category drop-down of the rows and changes it, as the owner does by picking an entry.</summary>
	private static async Task PickCategoryAsync(IRenderedComponent<TransactionsPage> page, string categoryId)
	{
		var picker = page.FindComponents<CategoryPicker>().First(p => string.Equals(p.Instance.TestId, "category-select", StringComparison.Ordinal));
		var dropDown = picker.FindComponent<RadzenDropDown<string>>();
		await page.InvokeAsync(() => dropDown.Instance.Change.InvokeAsync(categoryId)).ConfigureAwait(true);
	}
	#endregion

	#region Content Tests
	/// <summary>Verifies the tab starts on spending: the payment is listed with its amount, the salary is not, and the count and total say so.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Month_ListsSpendingWithCountAndTotal()
	{
		Serve(November());

		var page = Render<TransactionsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));
		page.Markup.Should().Contain(Formats.Belgian.Money(-12.50m)).And.NotContain("Acme Payroll");
		page.Find(".totals").TextContent.Should().Contain("1 transactions").And.Contain("Total out: " + Formats.Belgian.Money(12.50m));
	}

	/// <summary>Verifies a search that matches nothing shows the empty message and a zero count.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Search_NoMatch_ShowsEmptyMessage()
	{
		Serve(November());
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		await page.Find("input[aria-label='Search transactions']").InputAsync("zzz");

		page.Markup.Should().Contain("No transactions in this month.").And.NotContain("Shop One");
		page.Find(".totals").TextContent.Should().Contain("0 transactions");
	}

	/// <summary>Verifies the category in the address filters the rows, switching the kind filter to everything.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_CategoryInAddress_FiltersOnThatCategory()
	{
		Serve(November((user, first) => user.ReassignPayee(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"), confirmed: true)));

		Services.GetRequiredService<NavigationManager>().NavigateTo("/transactions?cat=trav");

		var page = Render<TransactionsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));
		page.Markup.Should().NotContain("Acme Payroll");
		page.Find(".totals").TextContent.Should().Contain("1 transactions");
	}
	#endregion

	#region Decision Tests
	/// <summary>Verifies picking a category sets it for the whole payee, and shows a notification that offers "Only this one".</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ChangeCategory_Picked_SendsPayeeCategoryAndOffersUndo()
	{
		Serve(November());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		await PickCategoryAsync(page, "trav");

		await host.WaitForAssertionAsync(() => host.Find("[data-testid='undo-category']"));
		Backend.LastDecisionBody.Should().Contain("\"payeeCategory\"").And.Contain("\"trav\"");
		host.Markup.Should().Contain("“Trips” set for all payments to Shop One");
	}

	/// <summary>Verifies "Only this one" restores the payee and sets the category of this payment alone.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task OnlyThisOne_Clicked_SendsTxnOnlyWithTheRestoreData()
	{
		Serve(November());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));
		await PickCategoryAsync(page, "trav");
		await host.WaitForAssertionAsync(() => host.Find("[data-testid='undo-category']"));

		await host.Find("[data-testid='undo-category']").ClickAsync();

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"txnOnly\""));
		Backend.LastDecisionBody.Should().Contain("\"t1\"").And.Contain("\"trav\"").And.Contain("\"restore\"").And.Contain("\"other\"");
		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("only this payment changed"));
	}

	/// <summary>Verifies a refused category change shows the backend's reason and offers no undo.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ChangeCategory_BackendRefuses_ShowsTheProblemWithoutUndo()
	{
		Serve(November());
		Backend.Respond(DecisionsRoute, HttpStatusCode.Conflict, """{"title":"Conflict","detail":"The database is not initialised."}""", "application/problem+json");
		var host = RenderRadzenHost();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		await PickCategoryAsync(page, "trav");

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("The database is not initialised."));
		host.FindAll("[data-testid='undo-category']").Should().BeEmpty();
	}

	/// <summary>Verifies the tick next to an automatic category confirms the payee's category.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Confirm_Clicked_SendsConfirmForTheSeries()
	{
		var analysis = November();
		Serve(analysis);
		AcceptDecisions();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		await page.Find("button[aria-label='Confirm the category of Shop One']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"confirm\"").And.Contain(TestWorkspace.SeriesKeyOf(analysis, "t1")));
	}

	/// <summary>Verifies the star button marks a payment as a fixed payment.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task MarkFixed_Clicked_SendsMarkForTheSeries()
	{
		var analysis = November();
		Serve(analysis);
		AcceptDecisions();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		await page.Find("button[aria-label='Mark Shop One as a fixed payment']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"mark\"").And.Contain(TestWorkspace.SeriesKeyOf(analysis, "t1")));
	}
	#endregion

	#region Split Tests
	/// <summary>Verifies the scissors button opens the split dialog, a split that does not add up cannot be saved, and a balanced one is sent.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Split_OpenedFromRow_ValidatesAndSendsTheParts()
	{
		Serve(November());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<TransactionsPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Shop One"));

		var opening = page.Find("button[aria-label^='Split this payment into parts']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("input[aria-label='Amount of part 2']"));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();
		await host.Find("input[aria-label='Amount of part 2']").InputAsync("5");
		await host.Find("input[aria-label='Amount of part 1']").InputAsync("1");
		host.Find("[role='status'].unbalanced").TextContent.Should().Contain("Left to assign: " + Formats.Belgian.Money(6.50m));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();

		await host.Find("input[aria-label='Amount of part 1']").InputAsync("7.50");
		host.Find("[role='status'].balanced").TextContent.Should().Contain("Everything is assigned");
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeFalse();
		await ButtonWithText(host, "Save").ClickAsync();
		await opening;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"split\"").And.Contain("\"t1\"").And.Contain("7.5"));
	}
	#endregion
}
