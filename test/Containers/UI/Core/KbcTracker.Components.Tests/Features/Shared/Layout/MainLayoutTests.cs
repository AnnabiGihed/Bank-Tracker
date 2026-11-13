using System.Net;

namespace KbcTracker.Components.Tests.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="MainLayout"/> and the parts it holds: the sync status, the tabs with their badges, the
///              month taken from <c>?m=</c>, the Sync button, the Connect link when no bank is connected, and the
///              <see cref="BackendProblem"/> text when the backend refuses or is not running.
/// </summary>
public sealed class MainLayoutTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds an analysis with a payment in October and one in November.</summary>
	private static Analysis TwoMonths() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 3), -8m, "Rail Co"),
	]);

	/// <summary>Renders the layout around a recognisable page body.</summary>
	private IRenderedComponent<MainLayout> RenderLayout() => Render<MainLayout>(p => p.Add(l => l.Body, "<p>page body</p>"));
	#endregion

	#region Frame Tests
	/// <summary>Verifies the frame shows the sync status, the month selector, the tabs and the page body once the workspace is loaded.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_LoadedWorkspace_ShowsStatusTabsMonthAndBody()
	{
		var analysis = TwoMonths();
		Serve(analysis);

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.Markup.Should().Contain("Synced"));
		layout.Find("header").TextContent.Should().Contain("history since 03/10/2026");
		layout.Find("[aria-label='Month']").Should().NotBeNull();
		layout.Find("nav[aria-label='Tabs']").TextContent.Should().Contain("Fixed payments").And.Contain("To review").And.Contain("Settings");
		layout.Find("main").TextContent.Should().Contain("page body");
	}

	/// <summary>Verifies the tab for review carries the number of open items as a badge and every tab keeps the selected month.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_OpenReviewItems_BadgeOnTheReviewTab()
	{
		var analysis = TwoMonths();
		analysis.Review.Count.Should().BeGreaterThan(0);
		Serve(analysis);

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.FindAll("nav a").Should().NotBeEmpty());
		var review = layout.FindAll("nav a").Single(a => a.TextContent.Contains("To review", StringComparison.Ordinal));
		review.TextContent.Should().Contain(Formats.Belgian.Number(analysis.Review.Count));
		review.GetAttribute("href").Should().Be("/review?m=2026-11");
	}

	/// <summary>Verifies the month in the address is the one the frame selects, and the other months are one step away.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_MonthInAddress_SelectsThatMonth()
	{
		Serve(TwoMonths());
		Services.GetRequiredService<NavigationManager>().NavigateTo("/overview?m=2026-10");

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.Find("header").TextContent.Should().Contain("October 2026"));
		layout.Find("a[aria-label='Newer month']").GetAttribute("href").Should().Contain("m=2026-11");
		layout.FindAll("span.step.disabled").Should().ContainSingle();
	}

	/// <summary>Verifies a workspace without a connected bank offers to connect one instead of Sync.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NotConnected_OffersConnectBank()
	{
		Serve(TwoMonths(), connected: false);

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.Find("header").TextContent.Should().Contain("Not connected"));
		layout.Find("header a[href='/banks']").TextContent.Should().Contain("Connect bank");
		layout.FindAll("header button").Should().NotContain(b => b.TextContent.Contains("Sync", StringComparison.Ordinal));
	}
	#endregion

	#region Sync Tests
	/// <summary>Verifies the Sync button asks the backend to sync and tells the owner how many transactions came in.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Sync_Clicked_PostsToTheBackendAndReportsTheResult()
	{
		Serve(TwoMonths());
		Backend.Respond("/api/v1/sync", HttpStatusCode.OK, """{"added":3}""");
		var host = RenderRadzenHost();
		var layout = RenderLayout();
		await layout.WaitForAssertionAsync(() => ButtonWithText(layout, "↻ Sync"));

		await ButtonWithText(layout, "↻ Sync").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("3 new transaction(s)."));
		Backend.Count("POST", "/api/v1/sync").Should().Be(1);
	}

	/// <summary>Verifies a sync that failed is shown as a notification with the backend's own words.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Sync_BackendRefuses_ShowsTheProblem()
	{
		Serve(TwoMonths());
		Backend.Respond("/api/v1/sync", HttpStatusCode.Conflict, """{"title":"Conflict","detail":"A sync is already running."}""", "application/problem+json");
		var host = RenderRadzenHost();
		var layout = RenderLayout();
		await layout.WaitForAssertionAsync(() => ButtonWithText(layout, "↻ Sync"));

		await ButtonWithText(layout, "↻ Sync").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("A sync is already running."));
	}
	#endregion

	#region Error State Tests
	/// <summary>Verifies a backend that is not running shows the unavailable problem with what to do.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_BackendUnreachable_ShowsTheUnavailableProblem()
	{
		Backend.Unreachable = true;

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.Find("[role='alert']").TextContent.Should().Contain("Backend unavailable.").And.Contain("Start the backend and check its connection settings."));
		layout.Find("main").TextContent.Should().Contain("page body");
	}

	/// <summary>Verifies a backend that refuses the web app shows the access problem.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_BackendForbids_ShowsTheAccessProblem()
	{
		Backend.Respond("/api/v1/workspace", HttpStatusCode.Forbidden, "{}");

		var layout = RenderLayout();

		await layout.WaitForAssertionAsync(() => layout.Find("[role='alert']").TextContent.Should().Contain("Backend refused access.").And.Contain("Backend:OwnerLogin"));
	}

	/// <summary>Verifies "Try again" reads the workspace again and the problem goes away once the backend answers.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Retry_BackendBack_ClearsTheProblem()
	{
		Backend.Unreachable = true;
		var layout = RenderLayout();
		await layout.WaitForAssertionAsync(() => layout.Markup.Should().Contain("Backend unavailable."));
		Backend.Unreachable = false;
		Serve(TwoMonths());

		await ButtonWithText(layout, "Try again").ClickAsync();

		await layout.WaitForAssertionAsync(() => layout.Markup.Should().NotContain("Backend unavailable.").And.Contain("Synced"));
	}
	#endregion
}
