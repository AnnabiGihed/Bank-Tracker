using System.Net;

namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reports that the backend answered a read with a refusal, carrying its status and its safe problem
///              detail, so a client can show why instead of a guess. A missing answer stays a plain
///              <see cref="HttpRequestException"/>.
/// </summary>
public sealed class BackendRefusedException : HttpRequestException
{
	#region Constructors
	/// <summary>Initialises a new <see cref="BackendRefusedException"/> with a generic message.</summary>
	public BackendRefusedException()
		: base("The backend refused the request.")
	{
	}

	/// <summary>Initialises a new <see cref="BackendRefusedException"/> without a status.</summary>
	/// <param name="message">The backend's safe problem detail.</param>
	public BackendRefusedException(string message)
		: base(message)
	{
	}

	/// <summary>Initialises a new <see cref="BackendRefusedException"/> wrapping another failure.</summary>
	/// <param name="message">The backend's safe problem detail.</param>
	/// <param name="innerException">The failure behind the refusal.</param>
	public BackendRefusedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Initialises a new <see cref="BackendRefusedException"/> for an answer the backend gave.</summary>
	/// <param name="message">The backend's safe problem detail, shown to the owner as is.</param>
	/// <param name="statusCode">The backend's status.</param>
	public BackendRefusedException(string message, HttpStatusCode statusCode)
		: base(message, null, statusCode)
	{
	}
	#endregion
}
