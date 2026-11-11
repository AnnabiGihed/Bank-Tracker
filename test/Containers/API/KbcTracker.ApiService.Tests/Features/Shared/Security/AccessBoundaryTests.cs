using System.Net;
using KbcTracker.ApiService.Features.Shared.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KbcTracker.ApiService.Tests.Features.Shared.Security;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies client-supplied identity cannot bypass the trusted loopback proxy boundary.
/// </summary>
public sealed class AccessBoundaryTests
{
	#region Access Tests
	/// <summary>Verifies remote requests cannot spoof the owner's identity header.</summary>
	/// <param name="environment">The host environment.</param>
	/// <param name="remoteAddress">The actual TCP peer.</param>
	/// <param name="identity">The presented identity header.</param>
	/// <param name="allowed">Whether dispatch is permitted.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("Development", "127.0.0.1", "", true)]
	[InlineData("Development", "192.0.2.1", ApiFixture.OwnerLogin, false)]
	[InlineData("Production", "127.0.0.1", ApiFixture.OwnerLogin, true)]
	[InlineData("Production", "127.0.0.1", "", false)]
	[InlineData("Production", "127.0.0.1", "different@example.invalid", false)]
	[InlineData("Production", "192.0.2.1", ApiFixture.OwnerLogin, false)]
	public async Task Identity_RequiresTrustedPeerAndOwner(string environment, string remoteAddress, string identity, bool allowed)
	{
		using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
		var host = Substitute.For<IWebHostEnvironment>();
		host.EnvironmentName.Returns(environment);
		var context = new DefaultHttpContext { RequestServices = services };
		await using var body = new MemoryStream();
		context.Response.Body = body;
		context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
		context.Request.Path = "/api/v1/connection";
		context.Request.Headers[TailnetAccessMiddleware.IdentityHeader] = identity;
		var dispatched = false;
		var middleware = new TailnetAccessMiddleware(_ => { dispatched = true; return Task.CompletedTask; }, host,
			Options.Create(new BackendAccessOptions { OwnerLogin = ApiFixture.OwnerLogin }));

		await middleware.InvokeAsync(context);

		dispatched.Should().Be(allowed);
		context.Response.StatusCode.Should().Be(allowed ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden);
	}

	/// <summary>Verifies cross-origin browser writes cannot use the proxy identity to trigger sync.</summary>
	/// <param name="origin">The browser origin, or an empty value for a non-browser client.</param>
	/// <param name="site">The browser's site relationship.</param>
	/// <param name="allowed">Whether the request may reach the endpoint.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("https://untrusted.example.invalid", "", false)]
	[InlineData("http://localhost", "same-origin", true)]
	[InlineData("", "cross-site", false)]
	[InlineData("", "", true)]
	public async Task BrowserWrite_CrossOrigin_Rejected(string origin, string site, bool allowed)
	{
		using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
		var host = Substitute.For<IWebHostEnvironment>();
		host.EnvironmentName.Returns("Production");
		var context = new DefaultHttpContext { RequestServices = services };
		await using var body = new MemoryStream();
		context.Response.Body = body;
		context.Connection.RemoteIpAddress = IPAddress.Loopback;
		context.Request.Host = new HostString("localhost");
		context.Request.Method = HttpMethods.Post;
		context.Request.Path = "/api/v1/sync";
		context.Request.Headers[TailnetAccessMiddleware.IdentityHeader] = ApiFixture.OwnerLogin;
		context.Request.Headers.Origin = origin;
		context.Request.Headers["Sec-Fetch-Site"] = site;
		var dispatched = false;
		var middleware = new TailnetAccessMiddleware(_ => { dispatched = true; return Task.CompletedTask; }, host,
			Options.Create(new BackendAccessOptions { OwnerLogin = ApiFixture.OwnerLogin }));

		await middleware.InvokeAsync(context);

		dispatched.Should().Be(allowed);
	}
	#endregion
}
