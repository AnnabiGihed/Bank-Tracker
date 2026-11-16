using KbcTracker.Shared.Features.Backend;
using KbcTracker.Web.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ApiProgram = KbcTracker.ApiService.Program;

namespace KbcTracker.E2E.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Wires the two hosts for tests: the API host gets a test secret and throw-away key files, and the web host
///              reaches that API in memory with its own token handler, which fetches the <c>web</c> client's real
///              client-credentials token from the API's token endpoint.
/// </summary>
internal static class ApiSignIn
{
	#region Constants
	/// <summary>The secret of the <c>web</c> client in tests.</summary>
	public const string WebSecret = "test-web-client-secret-not-used-anywhere-else";
	#endregion

	#region Public Methods
	/// <summary>Gives an API host the test client secret and the test key files.</summary>
	/// <param name="builder">The API host's builder.</param>
	/// <returns>The same builder.</returns>
	public static IWebHostBuilder UseTestSignIn(this IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder
			.UseSetting("Authentication:Clients:Web:Secret", WebSecret)
			.UseSetting("Authentication:Signing:Path", TestCertificates.SigningPath)
			.UseSetting("Authentication:Encryption:Path", TestCertificates.EncryptionPath);
	}

	/// <summary>Connects a web host to an API host in memory: the shared client secret, the backend address, an HTTP transport
	/// to the API for the backend client and for the token client, and the test-only sign-in route (see
	/// <see cref="TestSignInStartupFilter"/>). The web host's own token handler then fetches the real client-credentials token.</summary>
	/// <param name="builder">The web host's builder.</param>
	/// <param name="api">The API host that issues the token and serves the backend calls.</param>
	/// <returns>The same builder.</returns>
	public static IWebHostBuilder UseApiHost(this IWebHostBuilder builder, WebApplicationFactory<ApiProgram> api)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(api);
		return builder
			.UseSetting("Backend:BaseUrl", "http://localhost/")
			.UseSetting(WebAuthentication.SecretSetting, WebSecret)
			.ConfigureTestServices(services =>
			{
				services.AddHttpClient<BackendClient>().ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
				services.AddHttpClient(ApiTokenClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
				services.AddTransient<IStartupFilter, TestSignInStartupFilter>();
			});
	}
	#endregion
}
