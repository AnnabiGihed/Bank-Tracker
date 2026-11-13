using System.Net;

namespace KbcTracker.Components.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="BankPickerPage"/>: the banks with KBC first, Connect that starts the consent and
///              hands the bank's address to the host's launcher, a refused consent shown as a notification, and a host
///              that cannot launch the bank's page.
/// </summary>
public sealed class BankPickerPageTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Makes the backend offer two banks, KBC second.</summary>
	private void OfferBanks() => Backend.Respond("/api/v1/banks", HttpStatusCode.OK, """[{"name":"Argenta","country":"BE"},{"name":"KBC Brussels","country":"BE"}]""");
	#endregion

	#region Content Tests
	/// <summary>Verifies the banks are listed with KBC first.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_Banks_KbcFirst()
	{
		OfferBanks();

		var page = Render<BankPickerPage>();

		await page.WaitForAssertionAsync(() => page.FindAll("tbody tr").Should().HaveCount(2));
		page.FindAll("tbody tr").Select(r => r.TextContent).First().Should().Contain("KBC Brussels");
		page.Markup.Should().Contain("Choose your bank");
	}

	/// <summary>Verifies a backend that is not running shows the problem and no banks.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Render_BackendUnreachable_ShowsTheProblem()
	{
		Backend.Unreachable = true;

		var page = Render<BankPickerPage>();

		await page.WaitForAssertionAsync(() => page.Find("[role='alert']").TextContent.Should().Contain("Backend unavailable."));
		page.FindAll("tbody tr").Should().BeEmpty();
	}
	#endregion

	#region Connect Tests
	/// <summary>Verifies Connect starts the consent for that bank and gives the bank's address to the launcher.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Connect_Clicked_LaunchesTheBanksAddress()
	{
		OfferBanks();
		Backend.Respond("/api/v1/consent", HttpStatusCode.OK, """{"authorizationUrl":"https://bank.example/auth?state=abc"}""");
		var page = Render<BankPickerPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Connect KBC Brussels']"));

		await page.Find("button[aria-label='Connect KBC Brussels']").ClickAsync();

		await page.WaitForAssertionAsync(() => { _ = Launcher.Received(1).LaunchAsync(Arg.Is<Uri>(u => u.Host == "bank.example"), Arg.Any<CancellationToken>()); });
		Backend.Count("POST", "/api/v1/consent").Should().Be(1);
	}

	/// <summary>Verifies a refused consent is shown and the launcher is not called.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Connect_BackendRefuses_ShowsTheProblemAndDoesNotLaunch()
	{
		OfferBanks();
		Backend.Respond("/api/v1/consent", HttpStatusCode.Conflict, """{"title":"Conflict","detail":"Bank access is not set up."}""", "application/problem+json");
		var host = RenderRadzenHost();
		var page = Render<BankPickerPage>();
		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Connect KBC Brussels']"));

		await page.Find("button[aria-label='Connect KBC Brussels']").ClickAsync();

		await host.WaitForAssertionAsync(() => host.Markup.Should().Contain("Bank access is not set up."));
		_ = Launcher.DidNotReceive().LaunchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
	}

	/// <summary>Verifies a host that cannot open the bank turns Connect off and shows its message.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Connect_HostCannotLaunch_ShowsTheMessageAndDisablesConnect()
	{
		OfferBanks();
		Launcher.CanLaunch.Returns(false);
		Launcher.UnavailableMessage.Returns("Reconnect from the web app on the PC.");

		var page = Render<BankPickerPage>();

		await page.WaitForAssertionAsync(() => page.Find("button[aria-label='Connect KBC Brussels']").HasAttribute("disabled").Should().BeTrue());
		page.Find("p[role='status']").TextContent.Should().Be("Reconnect from the web app on the PC.");
	}
	#endregion
}
