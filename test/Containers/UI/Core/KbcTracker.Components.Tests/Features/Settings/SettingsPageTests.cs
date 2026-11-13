using System.Net;

namespace KbcTracker.Components.Tests.Features.Settings;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SettingsPage"/>: the connection details, Sync now and Sync full history, Reconnect that
///              is off with the launcher's own message where the host cannot open the bank, the history figures, and the
///              CSV link that appears only when the host offers one.
/// </summary>
public sealed class SettingsPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds an analysis with payments in August and November.</summary>
	private static Analysis History() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 8, 3), -5m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 3), -6m, "Shop One"),
	]);
	#endregion

	#region Content Tests
	/// <summary>Verifies the connection and the history are shown with the view model's formatted figures.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Connected_ShowsConnectionAndHistory()
	{
		Serve(History());

		var page = Render<SettingsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Bank access valid until"));
		var text = page.Markup;
		text.Should().Contain(Formats.Belgian.LocalFullDate(new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero)));
		text.Should().Contain(Formats.Belgian.LocalFullDayTime(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero)));
		text.Should().Contain("03/08/2026 → 03/11/2026").And.Contain(SettingsViewModel.StorageText);
		page.Find("a[href='/export.csv']").TextContent.Should().Contain("Export CSV");
	}

	/// <summary>Verifies a workspace without a connection says so and offers to connect.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_NotConnected_OffersToConnect()
	{
		Serve(History(), connected: false);

		var page = Render<SettingsPage>();

		await page.WaitForAssertionAsync(() => page.Markup.Should().Contain("Not connected."));
		ButtonWithText(page, "Connect KBC Brussels").HasAttribute("disabled").Should().BeFalse();
		page.FindAll("button").Should().NotContain(b => b.TextContent.Contains("Sync now", StringComparison.Ordinal));
	}
	#endregion

	#region Sync Tests
	/// <summary>Verifies Sync now and Sync full history post to the backend, the second asking for the longest history.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Sync_NowAndFull_PostToTheBackend()
	{
		Serve(History());
		Backend.Respond("/api/v1/sync", HttpStatusCode.OK, """{"added":0}""");
		var page = Render<SettingsPage>();
		await page.WaitForAssertionAsync(() => ButtonWithText(page, "↻ Sync now"));

		await ButtonWithText(page, "↻ Sync now").ClickAsync();
		await page.WaitForAssertionAsync(() => Backend.Count("POST", "/api/v1/sync").Should().Be(1));
		await ButtonWithText(page, "Sync full history").ClickAsync();

		await page.WaitForAssertionAsync(() => Backend.Count("POST", "/api/v1/sync").Should().Be(2));
	}
	#endregion

	#region Reconnect Tests
	/// <summary>Verifies Reconnect opens the bank picker where the host can launch the bank's page.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Reconnect_HostCanLaunch_OpensTheBankPicker()
	{
		Serve(History());
		var page = Render<SettingsPage>();
		await page.WaitForAssertionAsync(() => ButtonWithText(page, "Reconnect / add account"));

		await ButtonWithText(page, "Reconnect / add account").ClickAsync();

		Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/banks");
	}

	/// <summary>Verifies Reconnect is off and the launcher's own message is shown where the host cannot open the bank.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Reconnect_HostCannotLaunch_ShowsTheLauncherMessage()
	{
		Launcher.CanLaunch.Returns(false);
		Launcher.UnavailableMessage.Returns("Reconnect from the web app on the PC.");
		Serve(History());

		var page = Render<SettingsPage>();

		await page.WaitForAssertionAsync(() => page.Find("p[role='status']").TextContent.Should().Be("Reconnect from the web app on the PC."));
		ButtonWithText(page, "Reconnect / add account").HasAttribute("disabled").Should().BeTrue();
	}
	#endregion
}
