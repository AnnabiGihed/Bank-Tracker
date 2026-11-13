using System.Net;

namespace KbcTracker.Components.Tests.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoriesPage"/>: the tree with roll-up totals per type, expanding a row to see its
///              sub-categories, the link to a category's transactions, and the dialog that creates, edits and deletes a
///              category, with the decision each one sends.
/// </summary>
public sealed class CategoriesPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>The route decisions are sent to.</summary>
	private const string DecisionsRoute = "/api/v1/decisions";

	/// <summary>Builds payments in Trips and its child Rail trips for September and October.</summary>
	private static Analysis Trips() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("s1", new DateOnly(2026, 9, 5), -5m, "Kiosk"),
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
	], (user, first) =>
	{
		user.ReassignPayee(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"), confirmed: true);
		user.ReassignPayee(TestWorkspace.SeriesKeyOf(first, "s1"), new CategoryId("trav"), confirmed: true);
	});

	/// <summary>Makes the backend accept decisions.</summary>
	private void AcceptDecisions() => Backend.Respond(DecisionsRoute, HttpStatusCode.OK, "{}");
	#endregion

	#region Tree Tests
	/// <summary>Verifies a card per type, the roll-up total of a root, and that a child is hidden until its parent is expanded.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Tree_RollsUpAndExpands()
	{
		Serve(Trips());
		SelectMonth("2026-10");

		var page = Render<CategoriesPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Trips"));
		page.FindAll("h2").Select(h => h.TextContent).Should().Contain(t => t.Contains("Spending", StringComparison.Ordinal));
		var trips = page.FindAll("tbody tr").Single(r => r.TextContent.Contains("Trips", StringComparison.Ordinal));
		trips.TextContent.Should().Contain(Formats.Belgian.Money(8m));
		page.Markup.Should().NotContain("Rail trips");

		await page.Find("button[aria-label='Show or hide the sub-categories of Trips']").ClickAsync();

		var rail = page.FindAll("tbody tr").Single(r => r.TextContent.Contains("Rail trips", StringComparison.Ordinal));
		rail.TextContent.Should().Contain(Formats.Belgian.Money(8m));
		page.Find("button[aria-label='Show or hide the sub-categories of Trips']").GetAttribute("aria-expanded").Should().Be("true");
	}

	/// <summary>Verifies a category's name links to its transactions in the same month.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_CategoryName_LinksToItsTransactions()
	{
		Serve(Trips());
		SelectMonth("2026-10");

		var page = Render<CategoriesPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Trips"));
		page.Find("a[href='/transactions?m=2026-10&cat=trav']").TextContent.Should().Contain("Trips");
	}
	#endregion

	#region Dialog Tests
	/// <summary>Verifies "+ New category" opens the dialog, and Save sends the new category as a spending root.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task NewCategory_Saved_SendsSaveCategory()
	{
		Serve(Trips());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<CategoriesPage>();
		await page.WaitForAssertionAsync(() => ButtonWithText(page, "+ New category"));

		var opening = ButtonWithText(page, "+ New category").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("input#category-name"));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();
		await host.Find("input#category-name").InputAsync("Pets");
		await host.Find("textarea#category-keywords").ChangeAsync("VET, PETSHOP");
		await ButtonWithText(host, "Save").ClickAsync();
		await opening;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"saveCategory\"").And.Contain("Pets").And.Contain("Expense").And.Contain("VET, PETSHOP"));
	}

	/// <summary>Verifies a category without a name cannot be saved.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task NewCategory_EmptyName_SaveBlocked()
	{
		Serve(Trips());
		SelectMonth("2026-10");
		var host = RenderRadzenHost();
		var page = Render<CategoriesPage>();
		await page.WaitForAssertionAsync(() => ButtonWithText(page, "+ New category"));
		_ = ButtonWithText(page, "+ New category").ClickAsync();
		await host.WaitForAssertionAsync(() => host.Find("input#category-name"));

		await host.Find("input#category-name").InputAsync(" ");

		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();
	}

	/// <summary>Verifies "+ sub-category" starts a child of that category.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SubCategory_Saved_SendsTheParent()
	{
		Serve(Trips());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<CategoriesPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Add a sub-category to Trips']"));

		var opening = page.Find("button[aria-label='Add a sub-category to Trips']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Find("input#category-name"));
		await host.Find("input#category-name").InputAsync("Plane trips");
		await ButtonWithText(host, "Save").ClickAsync();
		await opening;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"saveCategory\"").And.Contain("Plane trips").And.Contain("\"trav\""));
	}

	/// <summary>Verifies a category the owner made is deleted only after the confirmation, and the decision is sent.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Delete_Confirmed_SendsDeleteCategory()
	{
		Serve(Trips());
		SelectMonth("2026-10");
		AcceptDecisions();
		var host = RenderRadzenHost();
		var page = Render<CategoriesPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Edit Trips']"));
		var editing = page.Find("button[aria-label='Edit Trips']").ClickAsync();
		await host.WaitForAssertionAsync(() => ButtonWithText(host, "Delete"));

		var deleting = ButtonWithText(host, "Delete").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("Delete this category?"));
		Backend.LastDecisionBody.Should().BeNull();
		await host.FindAll("button").Last(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync();
		await deleting;
		await editing;

		await host.WaitForAssertionAsync(() => Backend.LastDecisionBody.Should().Contain("\"deleteCategory\"").And.Contain("\"trav\""));
	}

	/// <summary>Verifies a built-in category offers no Delete.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Edit_BuiltInCategory_OffersNoDelete()
	{
		Serve(Trips());
		SelectMonth("2026-10");
		var host = RenderRadzenHost();
		var page = Render<CategoriesPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Edit Other']"));

		_ = page.Find("button[aria-label='Edit Other']").ClickAsync();

		await host.WaitForAssertionAsync(() => ButtonWithText(host, "Save"));
		host.FindAll("button").Should().NotContain(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal));
	}
	#endregion
}
