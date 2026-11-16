using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.Shared.Tests.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the registration of <see cref="BackendClient"/>: the base address rules and the
///              extension point for the access token handler, which both UI hosts rely on.
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

		/// <summary>Gets the authorization header of the last request, or <see langword="null"/> when it carried none.</summary>
		public string? LastAuthorization { get; private set; }
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastUri = request.RequestUri;
			LastAuthorization = request.Headers.Authorization?.ToString();
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"added":0}""", Encoding.UTF8, "application/json") });
		}
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Stands in for a host's token handler: sets a fixed bearer token on every request.
	/// </summary>
	private sealed class FixedTokenHandler : DelegatingHandler
	{
		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
			return base.SendAsync(request, cancellationToken);
		}
		#endregion
	}
	#endregion

	#region Registration Tests
	/// <summary>Verifies a valid base address is used for the requests and that no credentials are added by default.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task AddBackendClient_ValidOptions_RequestsGoToBaseAddressWithoutCredentials()
	{
		using var handler = new RecordingHandler();
		using var provider = Provider(new BackendOptions("http://127.0.0.1:5080/"), handler);

		var reply = await provider.GetRequiredService<BackendClient>().SyncAsync(full: false, CancellationToken.None);

		reply.IsSuccess.Should().BeTrue();
		handler.LastUri.Should().Be(new Uri("http://127.0.0.1:5080/api/v1/sync"));
		handler.LastAuthorization.Should().BeNull();
	}

	/// <summary>Verifies a host's token handler, added through the extension point, sets the access token on each request.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task WithAccessTokenHandler_HostHandler_TokenSentOnRequests()
	{
		using var handler = new RecordingHandler();
		var services = new ServiceCollection();
		services.AddTransient<FixedTokenHandler>();
		services.AddBackendClient(new BackendOptions("https://backend.example.invalid/"))
			.WithAccessTokenHandler<FixedTokenHandler>()
			.ConfigurePrimaryHttpMessageHandler(() => handler);
		using var provider = services.BuildServiceProvider();

		await provider.GetRequiredService<BackendClient>().SyncAsync(full: true, CancellationToken.None);

		handler.LastAuthorization.Should().Be("Bearer test-token");
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

		var noBuilder = () => BackendClientRegistration.WithAccessTokenHandler<FixedTokenHandler>(null!);

		noServices.Should().Throw<ArgumentNullException>();
		noOptions.Should().Throw<ArgumentNullException>();
		noBuilder.Should().Throw<ArgumentNullException>();
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
