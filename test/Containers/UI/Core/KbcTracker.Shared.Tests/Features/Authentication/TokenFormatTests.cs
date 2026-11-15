using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace KbcTracker.Shared.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the small pieces of the sign-in: the PKCE pair (RFC 7636, S256), the parsing of a token response
///              and the reading of the browser's callback.
/// </summary>
public sealed class TokenFormatTests
{
	#region PKCE Tests
	/// <summary>Verifies the challenge is the base64url SHA-256 of the verifier, and every pair is new.</summary>
	[Fact]
	public void Pkce_Create_ChallengeIsHashOfVerifier()
	{
		var first = PkceChallenge.Create();
		var second = PkceChallenge.Create();

		first.Verifier.Length.Should().BeInRange(43, 128);
		first.Verifier.Should().MatchRegex("^[A-Za-z0-9_-]+$");
		first.Challenge.Should().Be(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(first.Verifier))));
		second.Verifier.Should().NotBe(first.Verifier);
	}
	#endregion

	#region Token Response Tests
	/// <summary>Verifies a complete response is read.</summary>
	[Fact]
	public void Parse_CompleteResponse_ReadsAllFields()
	{
		var response = TokenResponse.Parse("""{"access_token":"a","refresh_token":"r","expires_in":600,"token_type":"Bearer"}""");

		response.Should().Be(new TokenResponse("a", "r", TimeSpan.FromMinutes(10)));
	}

	/// <summary>Verifies a response without a refresh token is accepted, with none.</summary>
	[Fact]
	public void Parse_NoRefreshToken_RefreshTokenIsNull() =>
		TokenResponse.Parse("""{"access_token":"a","expires_in":60}""").RefreshToken.Should().BeNull();

	/// <summary>Verifies a response without an access token, with a bad lifetime, or that is not an object is refused.</summary>
	/// <param name="json">The body.</param>
	[Theory]
	[InlineData("""{"expires_in":60}""")]
	[InlineData("""{"access_token":"","expires_in":60}""")]
	[InlineData("""{"access_token":"a"}""")]
	[InlineData("""{"access_token":"a","expires_in":0}""")]
	[InlineData("""{"access_token":"a","expires_in":"soon"}""")]
	[InlineData("[]")]
	[InlineData("not json")]
	public void Parse_Invalid_Throws(string json)
	{
		var act = () => TokenResponse.Parse(json);

		act.Should().Throw<InvalidOperationException>();
	}
	#endregion

	#region Callback Tests
	/// <summary>Verifies the code is returned when the state matches.</summary>
	[Fact]
	public void ReadCode_MatchingState_ReturnsCode() =>
		AuthorizationCallback.ReadCode(new Dictionary<string, string> { ["code"] = "c", ["state"] = "s" }, "s").Should().Be("c");

	/// <summary>Verifies a callback with another state, no state or no code is refused.</summary>
	/// <param name="state">The returned state, or <see langword="null"/> for none.</param>
	/// <param name="code">The returned code, or <see langword="null"/> for none.</param>
	[Theory]
	[InlineData("other", "c")]
	[InlineData(null, "c")]
	[InlineData("s", null)]
	public void ReadCode_WrongStateOrNoCode_Throws(string? state, string? code)
	{
		var values = new Dictionary<string, string>();
		if (state is not null)
		{
			values["state"] = state;
		}

		if (code is not null)
		{
			values["code"] = code;
		}

		var act = () => AuthorizationCallback.ReadCode(values, "s");

		act.Should().Throw<InvalidOperationException>();
	}

	/// <summary>Verifies an error answer is turned into a fixed message that does not repeat the provider's text.</summary>
	[Fact]
	public void ReadCode_ErrorAnswer_ThrowsFixedMessage()
	{
		var act = () => AuthorizationCallback.ReadCode(new Dictionary<string, string> { ["error"] = "access_denied", ["error_description"] = "Not allowed." }, "s");

		act.Should().Throw<InvalidOperationException>().Which.Message.Should().StartWith("Sign-in failed.").And.NotContain("Not allowed");
	}
	#endregion
}
