using System.Net;

namespace KbcTracker.ViewModels.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="BankPickerViewModel"/> against a stubbed backend: the banks come with KBC first and
///              the rest by name, a refused list shows the backend's reason, and starting a consent returns the bank's
///              address or the problem.
/// </summary>
public sealed class BankPickerViewModelTests
{
	#region Test Infrastructure
	/// <summary>The route the banks are read from.</summary>
	private const string BanksRoute = "/api/v1/banks";

	/// <summary>The banks the stub lists.</summary>
	private const string BanksJson = """[{"name":"Zeta Bank","country":"BE"},{"name":"Alpha Bank","country":"FR"},{"name":"KBC Brussels","country":"BE"},{"name":"kbc business","country":"BE"}]""";
	#endregion

	#region Picker Tests
	/// <summary>Verifies KBC banks come first, whatever their case, and the others follow by name.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_Banks_KbcFirstThenByName()
	{
		using var backend = new StubBackend();
		backend.Respond(BanksRoute, HttpStatusCode.OK, BanksJson);

		var result = await BankPickerViewModel.LoadAsync(backend.Client(), Formats.Belgian, CancellationToken.None);

		result.IsSuccess.Should().BeTrue();
		result.Value!.Banks.Select(b => b.Name).Should().Equal("KBC Brussels", "kbc business", "Alpha Bank", "Zeta Bank");
	}

	/// <summary>Verifies a refused list, such as bank access not set up on the backend, shows the backend's reason.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_Refused_ShowsTheBackendsReason()
	{
		using var backend = new StubBackend();
		backend.Respond(BanksRoute, HttpStatusCode.Conflict, """{"title":"Conflict","status":409,"detail":"Bank access is not set up on the backend."}""", "application/problem+json");

		var result = await BankPickerViewModel.LoadAsync(backend.Client(), Formats.Belgian, CancellationToken.None);

		result.IsSuccess.Should().BeFalse();
		result.Problem!.Status.Should().Be(409);
		result.Problem.Detail.Should().Be("Bank access is not set up on the backend.");
	}

	/// <summary>Verifies starting a consent returns the address of the bank's consent page.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task BeginConsentAsync_Started_ReturnsTheBanksAddress()
	{
		using var backend = new StubBackend();
		backend.Respond(BanksRoute, HttpStatusCode.OK, BanksJson);
		backend.Respond("/api/v1/consent", HttpStatusCode.OK, """{"authorizationUrl":"https://bank.example.invalid/auth?state=1"}""");
		var picker = (await BankPickerViewModel.LoadAsync(backend.Client(), Formats.Belgian, CancellationToken.None)).Value!;

		var result = await picker.BeginConsentAsync(picker.Banks[1], CancellationToken.None);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(new Uri("https://bank.example.invalid/auth?state=1"));
		backend.Count("POST", "/api/v1/consent").Should().Be(1);
	}

	/// <summary>Verifies a refused consent gives the backend's message instead of an address.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task BeginConsentAsync_Refused_ProblemWithTheMessage()
	{
		using var backend = new StubBackend();
		backend.Respond(BanksRoute, HttpStatusCode.OK, BanksJson);
		backend.Respond("/api/v1/consent", HttpStatusCode.Conflict, """{"title":"Conflict","status":409,"detail":"Bank access is not set up on the backend."}""", "application/problem+json");
		var picker = (await BankPickerViewModel.LoadAsync(backend.Client(), Formats.Belgian, CancellationToken.None)).Value!;

		var result = await picker.BeginConsentAsync(picker.Banks[0], CancellationToken.None);

		result.IsSuccess.Should().BeFalse();
		result.Value.Should().BeNull();
		result.Problem!.Detail.Should().Be("Bank access is not set up on the backend.");
	}
	#endregion
}
