using System.Net;

namespace KbcTracker.Components.Tests.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="ReviewPage"/>: the payees with an automatic category, the direction filter, the
///              per-row category and tick, and "Confirm all shown" after a confirmation.
/// </summary>
public sealed class ReviewPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds payees of both directions that all got an automatic category.</summary>
	private static Analysis Payees() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 6), -3m, "Bakery"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 10, 1), 40m, "Friend"),
	]);

	/// <summary>Makes the backend accept decisions.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, "{}");
	#endregion

	#region Content Tests
	/// <summary>Verifies every payee waiting for a confirmation is listed with its figures, and the filter narrows it by direction.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_PayeesWithAutomaticCategory_ListedAndFilteredByDirection()
	{
		var analysis = Payees();
		analysis.Review.Should().NotBeEmpty();
		Serve(analysis);

		var page = Render<ReviewPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Rail Co"));
		page.Markup.Should().Contain("Bakery").And.Contain(Formats.Belgian.Money(-8m));
		page.Find("p.muted").TextContent.Should().Be(ReviewViewModel.Explanation);
		var direction = page.FindComponent<RadzenDropDown<DirectionFilter>>();

		await page.InvokeAsync(() => direction.Instance.Change.InvokeAsync(DirectionFilter.In));

		page.Markup.Should().Contain("Friend").And.NotContain("Rail Co");
	}

	/// <summary>Verifies a workspace with nothing to review says so.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NothingToReview_ShowsTheEmptyMessage()
	{
		Serve(TestWorkspace.Analyse([]));

		var page = Render<ReviewPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain(ReviewViewModel.EmptyMessage));
	}
	#endregion

	#region Decision Tests
	/// <summary>Verifies the tick of a row confirms that payee's category.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Confirm_RowTicked_SendsConfirmForThePayee()
	{
		var analysis = Payees();
		Serve(analysis);
		AcceptDecisions();
		var page = Render<ReviewPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Confirm the category of Bakery']"));

		await page.Find("button[aria-label='Confirm the category of Bakery']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"confirm\"").And.Contain(TestWorkspace.SeriesKeyOf(analysis, "t2")));
	}

	/// <summary>Verifies another category for a payee is sent as a payee category decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ChangeCategory_Picked_SendsPayeeCategory()
	{
		Serve(Payees());
		AcceptDecisions();
		var page = Render<ReviewPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Bakery"));
		var picker = page.FindComponents<CategoryPicker>().First(p => string.Equals(p.Instance.AriaLabel, "Category of Bakery", StringComparison.Ordinal));

		await page.InvokeAsync(() => picker.FindComponent<RadzenDropDown<string>>().Instance.Change.InvokeAsync("trav"));

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"payeeCategory\"").And.Contain("\"trav\""));
	}

	/// <summary>Verifies "Confirm all shown" asks first and then sends one decision for the payees shown.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ConfirmShown_Confirmed_SendsOneDecisionForTheShownPayees()
	{
		Serve(Payees());
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<ReviewPage>();
		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Bakery"));

		var confirming = ButtonWithText(page, "✓ Confirm all shown").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("Confirm the category of 3 payees?"));
		Backend.LastDecisionBody.Should().BeNull();
		await ButtonWithText(host, "Confirm").ClickAsync();
		await confirming;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"confirm\"").And.Contain("\"keys\""));
	}
	#endregion
}
