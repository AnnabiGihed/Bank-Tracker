namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries an API operation's status and safe problem detail to either UI client.
/// </summary>
/// <typeparam name="T">The successful response contract.</typeparam>
/// <param name="StatusCode">The HTTP status returned by the backend.</param>
/// <param name="Value">The success body, when present.</param>
/// <param name="Error">The backend's safe problem detail on failure.</param>
public sealed record BackendReply<T>(int StatusCode, T? Value, string? Error)
{
	#region Properties
	/// <summary>Gets whether the backend accepted the operation.</summary>
	public bool IsSuccess => StatusCode is >= 200 and < 300;
	#endregion
}
