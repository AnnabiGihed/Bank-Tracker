using Microsoft.Playwright;

namespace KbcTracker.E2E.Tests.Features.Browser;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Proves in a real Chromium that the Blazor web app works end to end: the Overview loads over a live
///              circuit, a payment's category is changed through its drop-down, and the "Only this one" undo restores the
///              payee while the payment keeps the new category. The outcome is read from the database the API wrote.
///              Run only these tests with <c>dotnet test --filter Category=Browser</c>; a failure leaves a trace and a
///              screenshot in the <c>playwright-artifacts</c> folder next to the test assembly.
/// </summary>
/// <param name="fixture">The hosts and the browser shared by the class.</param>
[Trait("Category", "Browser")]
public sealed class WebSmokeTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>
{
	#region Smoke Tests
	/// <summary>Verifies the journey Overview, Transactions, change category, undo for this payment only.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Journey_ChangeCategoryThenOnlyThisOne_PaymentKeepsCategoryAndPayeeIsRestored()
	{
		var context = await fixture.NewContextAsync();
		await using (context.ConfigureAwait(false))
		{
			var page = await context.NewPageAsync();
			var pages = new TrackerPages(page);
			try
			{
				await pages.OpenOverviewAsync("2026-10");
				await pages.OpenTransactionsTabAsync();

				await pages.ChooseCategoryAsync("Clothing");
				await Assertions.Expect(pages.PayeeCategoryNotice).ToBeVisibleAsync();
				var afterChange = await fixture.ReadDecisionsAsync();
				afterChange.PayeeSeriesKeys.Should().ContainSingle("the change applies to the whole payee first");

				await pages.UndoCategory.ClickAsync();
				await Assertions.Expect(pages.OnlyThisPaymentDone).ToBeVisibleAsync();

				var afterUndo = await fixture.ReadDecisionsAsync();
				afterUndo.PayeeSeriesKeys.Should().BeEmpty("the payee assignment is restored to what it was");
				afterUndo.PaymentCategories.Should().ContainKey(BrowserFixture.PaymentId).WhoseValue.Should().Be("clothing-clothes");
			}
			catch (Exception)
			{
				await SaveFailureAsync(context, page);
				throw;
			}
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Keeps a trace and a screenshot of the failed run for the test output.</summary>
	/// <param name="context">The context whose trace to save.</param>
	/// <param name="page">The page to photograph.</param>
	/// <returns>A task that completes when both files are written, or saving them failed.</returns>
	private static async Task SaveFailureAsync(IBrowserContext context, IPage page)
	{
		try
		{
			Directory.CreateDirectory(BrowserFixture.ArtifactsFolder);
			await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(BrowserFixture.ArtifactsFolder, "journey.png"), FullPage = true }).ConfigureAwait(false);
			await context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(BrowserFixture.ArtifactsFolder, "journey-trace.zip") }).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is PlaywrightException or IOException or UnauthorizedAccessException)
		{
			// Keeping the evidence is best effort: a failure here must not hide the test failure it documents.
		}
	}
	#endregion
}
