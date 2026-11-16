using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The proof key for code exchange of one sign-in (RFC 7636, method S256): a random verifier the app keeps
///              and the challenge derived from it, which goes to the authorize endpoint. Only the app that made the
///              verifier can redeem the code, so a code intercepted on the way back is worthless.
/// </summary>
/// <param name="Verifier">The random secret, sent only to the token endpoint.</param>
/// <param name="Challenge">The SHA-256 hash of the verifier, base64url encoded, sent to the authorize endpoint.</param>
public sealed record PkceChallenge(string Verifier, string Challenge)
{
	#region Constants
	/// <summary>The number of random bytes of the verifier, which gives 43 characters, the shortest length RFC 7636 allows.</summary>
	private const int VerifierBytes = 32;
	#endregion

	#region Public Methods
	/// <summary>Creates a new verifier and its challenge.</summary>
	/// <returns>The proof key.</returns>
	public static PkceChallenge Create()
	{
		var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(VerifierBytes));
		return new PkceChallenge(verifier, Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
	}
	#endregion
}
