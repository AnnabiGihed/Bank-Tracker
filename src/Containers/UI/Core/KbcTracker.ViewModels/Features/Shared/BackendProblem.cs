using System.Net;
using System.Text.Json;

namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Describes a failed backend call in words the owner can act on, without local history or transport
///              details: a refusal (403) or conflict (409) says what the backend refused, anything else that the
///              backend is unavailable. A failed action carries the backend's own safe message.
/// </summary>
/// <param name="Status">The HTTP status the problem stands for: the backend's own, or 503 when it gave no answer.</param>
/// <param name="Title">The short heading.</param>
/// <param name="Detail">What went wrong and what to do.</param>
public sealed record BackendProblem(int Status, string Title, string Detail)
{
	#region Constants
	/// <summary>The detail shown when a conflict carries no reason.</summary>
	private const string NoReason = "The backend gave no reason. Check its log in the Aspire dashboard.";

	/// <summary>The detail shown when a failed action carries no message.</summary>
	private const string NoMessage = "The backend could not complete this request.";
	#endregion

	#region Public Methods
	/// <summary>Tells whether an exception is a backend failure to show (no answer, a refusal, a timeout, an unreadable
	/// answer) rather than a bug or a cancellation the caller asked for.</summary>
	/// <param name="exception">The exception to judge.</param>
	/// <param name="cancellationToken">The token of the call; a cancellation it requested is not a backend failure.</param>
	/// <returns><see langword="true"/> when <see cref="FromException"/> can describe it.</returns>
	public static bool Handles(Exception exception, CancellationToken cancellationToken) =>
		!cancellationToken.IsCancellationRequested && exception is HttpRequestException or JsonException or TaskCanceledException;

	/// <summary>Describes a failed read, or a backend that gave no answer. A conflict shows the backend's own detail,
	/// because conflicts have several causes (bank access not set up, an empty database, a running sync) and a guess
	/// sent the owner to the wrong fix.</summary>
	/// <param name="exception">The failure, as <see cref="Handles"/> accepts it.</param>
	/// <returns>The problem to show.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
	public static BackendProblem FromException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return (exception as HttpRequestException)?.StatusCode switch
		{
			HttpStatusCode.Forbidden => new(403, "Backend refused access.",
				"The backend did not accept this web app. Check that Backend:OwnerLogin matches the backend's owner, and that the web app runs on the same PC."),
			HttpStatusCode.Conflict => new(409, "The backend refused this request.",
				exception is BackendRefusedException { Message: { Length: > 0 } reason } ? reason : NoReason),
			_ => new(503, "Backend unavailable.", "Start the backend and check its connection settings."),
		};
	}

	/// <summary>Describes a failed action from the backend's reply.</summary>
	/// <typeparam name="T">The success contract of the reply.</typeparam>
	/// <param name="reply">The failed reply.</param>
	/// <returns>The problem to show, with the reply's status and safe message.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reply"/> is <see langword="null"/>.</exception>
	public static BackendProblem FromReply<T>(BackendReply<T> reply)
	{
		ArgumentNullException.ThrowIfNull(reply);
		return new(reply.StatusCode, "Request could not complete.", string.IsNullOrWhiteSpace(reply.Error) ? NoMessage : reply.Error);
	}
	#endregion
}
