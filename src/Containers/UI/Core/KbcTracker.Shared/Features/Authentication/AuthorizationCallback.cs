using System.Diagnostics;

namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reads what the browser handed back to the app after sign-in (the redirect's query values): it yields the
///              authorization code only when the state is the one the app sent, and turns an error answer into a fixed message.
/// </summary>
public static class AuthorizationCallback
{
	#region Public Methods
	/// <summary>Gets the authorization code from the callback values.</summary>
	/// <param name="values">The query values of the redirect, by name.</param>
	/// <param name="expectedState">The state the app sent with the authorize request.</param>
	/// <returns>The authorization code.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The API answered with an error, the state differs, or there is no code.</exception>
	public static string ReadCode(IReadOnlyDictionary<string, string> values, string expectedState)
	{
		ArgumentNullException.ThrowIfNull(values);
		if (values.TryGetValue("error", out var error))
		{
			// The provider's text is not shown: it is not ours to display. The code and description go to the Debug log only.
			Debug.WriteLine($"Sign-in callback error: {error} {(values.TryGetValue("error_description", out var description) ? description : string.Empty)}");
			throw new InvalidOperationException("Sign-in failed. Try again, and check the backend's log if it keeps failing.");
		}

		if (!values.TryGetValue("state", out var state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The sign-in answer does not belong to this request.");
		}

		return values.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code)
			? code
			: throw new InvalidOperationException("The sign-in answer carries no code.");
	}
	#endregion
}
