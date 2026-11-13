using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.Shared.Tests.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the registration of <see cref="BackendClient"/>: the base address rules and the owner
///              identity header, which both UI hosts rely on.
/// </summary>
public sealed class BackendClientRegistrationTests
{
	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers every request with a small success and remembers the request, in place of the backend.
	/// </summary>
	private sealed class RecordingHandler : HttpMessageHandler
	{
		#region Properties
		/// <summary>Gets the last request's absolute address.</summary>
		public Uri? LastUri { get; private set; }

		/// <summary>Gets the owner header of the last request, or <see langword="null"/> when it carried none.</summary>
		public string? LastOwner { get; private set; }
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastUri = request.RequestUri;
			LastOwner = request.Headers.TryGetValues(BackendOptions.OwnerHeader, out var values) ? values.Single() : null;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"added":0}""", Encoding.UTF8, "application/json") });
		}
		#endregion
	}
	#endregion

	#region Registration Tests
	/// <summary>Verifies a valid base address is used for the requests and an owner login is sent as the identity header.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task AddBackendClient_ValidOptions_RequestsGoToBaseAddressWithOwnerHeader()
	{
		using var handler = new RecordingHandler();
		using var provider = Provider(new BackendOptions("http://127.0.0.1:5080/", "owner@example.invalid"), handler);

		var reply = await provider.GetRequiredService<BackendClient>().SyncAsync(full: false, CancellationToken.None);

		reply.IsSuccess.Should().BeTrue();
		handler.LastUri.Should().Be(new Uri("http://127.0.0.1:5080/api/v1/sync"));
		handler.LastOwner.Should().Be("owner@example.invalid");
	}

	/// <summary>Verifies no owner header is sent when no owner is configured, or the login is blank.</summary>
	/// <param name="owner">The configured owner login.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task AddBackendClient_NoOwner_NoIdentityHeader(string? owner)
	{
		using var handler = new RecordingHandler();
		using var provider = Provider(new BackendOptions("https://backend.example.invalid/", owner), handler);

		await provider.GetRequiredService<BackendClient>().SyncAsync(full: true, CancellationToken.None);

		handler.LastOwner.Should().BeNull();
		handler.LastUri.Should().Be(new Uri("https://backend.example.invalid/api/v1/sync?full=true"));
	}

	/// <summary>Verifies a base address that is not an absolute HTTP address ending in a slash, without user info, is
	/// refused with a message that names the setting.</summary>
	/// <param name="address">The configured base address.</param>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("/relative/")]
	[InlineData("ftp://backend.example.invalid/")]
	[InlineData("http://backend.example.invalid/api")]
	[InlineData("http://user:secret@backend.example.invalid/")]
	public void AddBackendClient_InvalidBaseAddress_RefusedWhenClientIsCreated(string? address)
	{
		using var handler = new RecordingHandler();
		using var provider = Provider(new BackendOptions(address), handler);

		var act = () => provider.GetRequiredService<BackendClient>();

		act.Should().Throw<InvalidOperationException>().WithMessage("*Backend:BaseUrl*");
	}

	/// <summary>Verifies the registration refuses missing arguments.</summary>
	[Fact]
	public void AddBackendClient_NullArguments_Throw()
	{
		var services = new ServiceCollection();

		var noServices = () => BackendClientRegistration.AddBackendClient(null!, new BackendOptions("http://localhost/"));
		var noOptions = () => services.AddBackendClient(null!);

		noServices.Should().Throw<ArgumentNullException>();
		noOptions.Should().Throw<ArgumentNullException>();
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds a provider with the client registered and its transport replaced by the handler.</summary>
	private static ServiceProvider Provider(BackendOptions options, RecordingHandler handler)
	{
		var services = new ServiceCollection();
		services.AddBackendClient(options).ConfigurePrimaryHttpMessageHandler(() => handler);
		return services.BuildServiceProvider();
	}
	#endregion
}
