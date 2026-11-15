using KbcTracker.Web.Features.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace KbcTracker.E2E.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Checks two guards of the web host's sign-in that need no running API: that only a local path is accepted
///              as the place to return to after sign-in, and that the host refuses to start when the API's address is
///              neither https nor on this PC.
/// </summary>
public sealed class WebSignInGuardTests
{
	#region Return Address Tests
	/// <summary>Verifies the return address is kept for local paths and replaced by the root for everything else.</summary>
	/// <param name="returnUrl">The requested address.</param>
	/// <param name="expected">The address the sign-in returns to.</param>
	[Theory]
	[InlineData("/", "/")]
	[InlineData("/banks?x=1", "/banks?x=1")]
	[InlineData(null, "/")]
	[InlineData("", "/")]
	[InlineData("//evil.example", "/")]
	[InlineData("/\\evil.example", "/")]
	[InlineData("/%09/evil.example", "/%09/evil.example")]
	[InlineData("/\t/evil.example", "/")]
	[InlineData("/\n/evil.example", "/")]
	[InlineData("https://evil.example", "/")]
	[InlineData("evil.example", "/")]
	[InlineData("~/page", "/")]
	public void LocalOrRoot_Address_KeepsOnlyLocalPaths(string? returnUrl, string expected) =>
		SignInEndpoints.LocalOrRoot(returnUrl).Should().Be(expected);
	#endregion

	#region Address Tests
	/// <summary>Verifies a plain-HTTP address that is not on this PC stops the start, and https or loopback is accepted.</summary>
	/// <param name="backend">The configured backend address.</param>
	/// <param name="accepted">Whether the host may start.</param>
	[Theory]
	[InlineData("http://api.example.com/", false)]
	[InlineData("http://10.0.0.5/", false)]
	[InlineData("https://api.example.com/", true)]
	[InlineData("http://127.0.0.1:5080/", true)]
	[InlineData("http://localhost:5080/", true)]
	public void AddWebAuthentication_BackendAddress_RequiresHttpsUnlessLoopback(string backend, bool accepted)
	{
		var builder = WebApplication.CreateBuilder();
		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Backend:BaseUrl"] = backend,
			[WebAuthentication.SecretSetting] = "stub-secret",
		});

		var act = () => builder.AddWebAuthentication();

		if (accepted)
		{
			act.Should().NotThrow();
		}
		else
		{
			act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
		}
	}
	#endregion
}
