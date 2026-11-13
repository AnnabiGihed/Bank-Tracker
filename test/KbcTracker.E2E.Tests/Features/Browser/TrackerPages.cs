using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace KbcTracker.E2E.Tests.Features.Browser;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The page object of the web app: every selector the browser tests use, so a markup change is fixed in
///              one place. Roles and labels first, <c>data-testid</c> where the components provide it.
/// </summary>
/// <param name="page">The browser page to drive.</param>
public sealed class TrackerPages(IPage page)
{
	#region Properties
	/// <summary>Gets the month selector; it exists only once the Blazor circuit rendered the shell.</summary>
	public ILocator MonthSelector => page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Month" });

	/// <summary>Gets the category drop-down of the payments in the table.</summary>
	public ILocator CategorySelects => page.GetByTestId("category-select");

	/// <summary>Gets the "Only this one" button of the notification.</summary>
	public ILocator UndoCategory => page.GetByTestId("undo-category");

	/// <summary>Gets the notification that says a category now applies to a whole payee.</summary>
	public ILocator PayeeCategoryNotice => page.GetByText("set for all payments to");

	/// <summary>Gets the confirmation that only one payment changed.</summary>
	public ILocator OnlyThisPaymentDone => page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "only this payment changed" });
	#endregion

	#region Public Methods
	/// <summary>Opens the Overview of a month and waits until the circuit has rendered it.</summary>
	/// <param name="month">The month as <c>yyyy-MM</c>.</param>
	/// <returns>A task that completes when the month selector is visible.</returns>
	public async Task OpenOverviewAsync(string month)
	{
		await page.GotoAsync($"/overview?m={month}").ConfigureAwait(false);
		await Assertions.Expect(MonthSelector).ToBeVisibleAsync().ConfigureAwait(false);
	}

	/// <summary>Follows the Transactions tab.</summary>
	/// <returns>A task that completes when the payments table is visible.</returns>
	public async Task OpenTransactionsTabAsync()
	{
		await page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Tabs" })
			.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Transactions" }).ClickAsync().ConfigureAwait(false);
		await Assertions.Expect(CategorySelects.First).ToBeVisibleAsync().ConfigureAwait(false);
	}

	/// <summary>Chooses a category in the drop-down of the first payment.</summary>
	/// <param name="categoryName">The category's name, without its indentation.</param>
	/// <returns>A task that completes when the option was clicked.</returns>
	public async Task ChooseCategoryAsync(string categoryName)
	{
		await CategorySelects.First.ClickAsync().ConfigureAwait(false);
		await page.GetByRole(AriaRole.Searchbox).Or(page.Locator(".rz-dropdown-panel:visible input")).First.PressSequentiallyAsync(categoryName).ConfigureAwait(false);
		var option = page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { NameRegex = ExactName(categoryName) });
		await option.ClickAsync().ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>Matches an option whose text is the name, ignoring the whitespace around it.</summary>
	/// <param name="name">The category's name.</param>
	/// <returns>The pattern.</returns>
	private static Regex ExactName(string name) => new($@"^\s*{Regex.Escape(name)}\s*$", RegexOptions.None, TimeSpan.FromSeconds(1));
	#endregion
}
