using System.Net;

namespace KbcTracker.Components.Tests.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="FixedPaymentsPage"/>: the month's totals, the table with the status pill, the
///              suggestions with their accept and dismiss actions, the edit dialog and stopping a fixed payment after a
///              confirmation.
/// </summary>
public sealed class FixedPaymentsPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds a rent of 940 euro that was paid on the 3rd of September and of October.</summary>
	private static Analysis Rent() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("r1", new DateOnly(2026, 9, 3), -940m, "Landlord", "Rent"),
		TestWorkspace.Payment("r2", new DateOnly(2026, 10, 3), -940m, "Landlord", "Rent"),
	], (user, first) => user.AcceptFixedPayment(RecurringPayment.Create(new RecurringPaymentId("rent"), "Rent", new CategoryId("bills"),
		new PayeeMatch([TestWorkspace.SeriesKeyOf(first, "r1")], []), new RecurrenceRule(940m, 3m, 30), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value));

	/// <summary>Builds a gym membership of 13.99 euro paid on the 5th of three months in a row, not by card, which the analysis suggests as a fixed payment (not as a subscription).</summary>
	private static Analysis Membership() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("s1", new DateOnly(2026, 8, 5), -13.99m, "Gym", "Membership"),
		TestWorkspace.Payment("s2", new DateOnly(2026, 9, 5), -13.99m, "Gym", "Membership"),
		TestWorkspace.Payment("s3", new DateOnly(2026, 10, 5), -13.99m, "Gym", "Membership"),
	]);

	/// <summary>Makes the backend accept decisions.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, "{}");
	#endregion

	#region Content Tests
	/// <summary>Verifies the month's totals and the row of a fixed payment with its paid status.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_FixedPayment_ShowsTotalsAndPaidStatus()
	{
		Serve(Rent());
		SelectMonth("2026-10");

		var page = Render<FixedPaymentsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Rent"));
		page.Find("dl.summary").TextContent.Should().Contain("Expected in October 2026").And.Contain(Formats.Belgian.Money(940m));
		var row = page.FindAll("tr").Single(r => r.TextContent.Contains("Monthly", StringComparison.Ordinal));
		row.TextContent.Should().Contain("Rent").And.Contain("2 payment(s)").And.Contain("last 03/10/26").And.Contain("paid 03/10");
		row.QuerySelector(".pill.ok").Should().NotBeNull();
	}

	/// <summary>Verifies a workspace without confirmed fixed payments says how to add one.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NoFixedPayments_ShowsTheEmptyMessage()
	{
		Serve(TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -5m, "Kiosk")]));

		var page = Render<FixedPaymentsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain(FixedPaymentsViewModel.EmptyMessage));
	}
	#endregion

	#region Suggestion Tests
	/// <summary>Verifies a suggestion is shown with its amount, rhythm and the payments seen, and Accept sends the accept decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Suggestion_Accepted_SendsAccept()
	{
		var analysis = Membership();
		analysis.Suggestions.Should().NotBeEmpty();
		Serve(analysis);
		AcceptDecisions();

		var page = Render<FixedPaymentsPage>();

		await page.WaitForAssertionAsync(() => page.Find("article[aria-label^='Suggestion']"));
		var card = page.Find("article[aria-label^='Suggestion']").TextContent;
		card.Should().Contain("Gym").And.Contain(Formats.Belgian.Money(13.99m)).And.Contain("monthly");
		page.FindAll("article ul.chips li").Should().HaveCount(3);

		await page.Find("button[aria-label^='Accept']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"accept\"").And.Contain(analysis.Suggestions[0].Signature));
	}

	/// <summary>Verifies "Not fixed" sends the dismiss decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Suggestion_Dismissed_SendsDismiss()
	{
		var analysis = Membership();
		Serve(analysis);
		AcceptDecisions();
		var page = Render<FixedPaymentsPage>();
		await page.WaitForAssertionAsync(() => page.Find("article[aria-label^='Suggestion']"));

		await page.Find("button[aria-label$='is not a fixed payment']").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"dismiss\""));
	}
	#endregion

	#region Edit And Stop Tests
	/// <summary>Verifies Edit opens the dialog with the payment's values, and Save sends the update with the changed name and amount.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Edit_Saved_SendsUpdateFixed()
	{
		Serve(Rent());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<FixedPaymentsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Edit Rent']"));

		var editing = page.Find("button[aria-label='Edit Rent']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("input#fixed-name"));
		host.Find("input#fixed-name").GetAttribute("value").Should().Be("Rent");
		await host.Find("input#fixed-name").InputAsync("Flat rent");
		await ButtonWithText(host, "Save").ClickAsync();
		await editing;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"updateFixed\"").And.Contain("Flat rent").And.Contain("940"));
	}

	/// <summary>Verifies a fixed payment is stopped only after the owner confirms.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Stop_Confirmed_SendsRemoveFixed()
	{
		Serve(Rent());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<FixedPaymentsPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Stop tracking Rent']"));

		var stopping = page.Find("button[aria-label='Stop tracking Rent']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain(FixedPaymentsViewModel.StopTrackingPrompt));
		Backend.LastDecisionBody.Should().BeNull();
		await ButtonWithText(host, "Stop tracking").ClickAsync();
		await stopping;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"removeFixed\"").And.Contain("\"rent\""));
	}
	#endregion
}
