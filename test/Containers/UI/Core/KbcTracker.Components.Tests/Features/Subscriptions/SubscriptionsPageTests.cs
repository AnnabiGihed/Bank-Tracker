using System.Net;

namespace KbcTracker.Components.Tests.Features.Subscriptions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SubscriptionsPage"/>: the suggestions with accept and dismiss, the table with cost per
///              month and year and the status pill, the totals, the note that subscriptions still count as fixed payments,
///              the edit dialog, and stopping a subscription after a confirmation.
/// </summary>
public sealed class SubscriptionsPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds a streaming subscription of 13.99 euro paid on the 5th of three months in a row.</summary>
	private static List<Txn> Streaming() =>
	[
		TestWorkspace.Payment("s1", new DateOnly(2026, 8, 5), -13.99m, "Streamflix"),
		TestWorkspace.Payment("s2", new DateOnly(2026, 9, 5), -13.99m, "Streamflix"),
		TestWorkspace.Payment("s3", new DateOnly(2026, 10, 5), -13.99m, "Streamflix"),
	];

	/// <summary>Builds the same history with the subscription accepted.</summary>
	private static Analysis Accepted() => TestWorkspace.Analyse(Streaming(), (user, first) =>
		user.AcceptFixedPayment(Analysis.ToDefinition(first.Suggestions.Single(), new RecurringPaymentId("stream"), new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)).Value));

	/// <summary>Makes the backend accept decisions.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, "{}");
	#endregion

	#region Content Tests
	/// <summary>Verifies the row, the totals and the note about fixed payments.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Subscription_ShowsRowTotalsAndNote()
	{
		Serve(Accepted());
		SelectMonth("2026-10");

		var page = Render<SubscriptionsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Streamflix"));
		page.Find("dl.summary").TextContent.Should().Contain(Formats.Belgian.Money(13.99m)).And.Contain(Formats.Belgian.Money(167.88m));
		var row = page.FindAll("tr").Single(r => r.TextContent.Contains("Streamflix", StringComparison.Ordinal));
		row.TextContent.Should().Contain("05/10/26").And.Contain("04/11/26").And.Contain("paid 05/10");
		page.Markup.Should().Contain(SubscriptionsViewModel.FixedNote);
	}

	/// <summary>Verifies the empty state says how to get a subscription.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NoSubscriptions_ShowsTheEmptyMessage()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -5m, "Kiosk")]));

		var page = Render<SubscriptionsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain(SubscriptionsViewModel.EmptyMessage));
	}
	#endregion

	#region Suggestion Tests
	/// <summary>Verifies a suggestion is shown and Accept sends the accept decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Suggestion_Accepted_SendsAccept()
	{
		var analysis = TestWorkspace.Analyse(Streaming());
		Serve(analysis);
		AcceptDecisions();
		var page = Render<SubscriptionsPage>();
		await page.WaitForAssertionAsync(() => page.Find("article[aria-label^='Suggestion']"));
		page.Find("article[aria-label^='Suggestion']").TextContent.Should().Contain("Streamflix").And.Contain("monthly");

		await page.Find("button[aria-label^='Accept']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"accept\"").And.Contain(analysis.Suggestions[0].Signature));
	}

	/// <summary>Verifies "Not fixed" sends the dismiss decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Suggestion_Dismissed_SendsDismiss()
	{
		Serve(TestWorkspace.Analyse(Streaming()));
		AcceptDecisions();
		var page = Render<SubscriptionsPage>();
		await page.WaitForAssertionAsync(() => page.Find("article[aria-label^='Suggestion']"));

		await page.Find("button[aria-label$='is not a fixed payment']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"dismiss\""));
	}
	#endregion

	#region Edit And Stop Tests
	/// <summary>Verifies Save in the edit dialog sends the update with the changed name.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Edit_Saved_SendsUpdateFixed()
	{
		Serve(Accepted());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<SubscriptionsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Edit Streamflix']"));

		var editing = page.Find("button[aria-label='Edit Streamflix']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("input#fixed-name"));
		await host.Find("input#fixed-name").InputAsync("Streamflix HD");
		await ButtonWithText(host, "Save").ClickAsync();
		await editing;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"updateFixed\"").And.Contain("Streamflix HD"));
	}

	/// <summary>Verifies a subscription is stopped only after the owner confirms.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Stop_Confirmed_SendsRemoveFixed()
	{
		Serve(Accepted());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<SubscriptionsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Stop tracking Streamflix']"));

		var stopping = page.Find("button[aria-label='Stop tracking Streamflix']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain(SubscriptionsViewModel.StopTrackingPrompt));
		Backend.LastDecisionBody.Should().BeNull();
		await ButtonWithText(host, "Stop tracking").ClickAsync();
		await stopping;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"removeFixed\""));
	}
	#endregion
}
