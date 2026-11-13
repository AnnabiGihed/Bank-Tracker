namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs a backend call and turns every backend failure into a <see cref="BackendResult{T}"/> the UI can
///              show, so no component handles transport exceptions itself. A cancellation the caller asked for, and
///              any other exception, still propagate.
/// </summary>
public static class BackendCall
{
	#region Public Methods
	/// <summary>Runs a read that throws on failure.</summary>
	/// <typeparam name="T">The value the call returns.</typeparam>
	/// <param name="call">The call to run.</param>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The value, or the problem to show.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="call"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
	public static async Task<BackendResult<T>> RunAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(call);
		try
		{
			return new BackendResult<T>(await call(cancellationToken).ConfigureAwait(false), null);
		}
		catch (Exception exception) when (BackendProblem.Handles(exception, cancellationToken))
		{
			return new BackendResult<T>(default, BackendProblem.FromException(exception));
		}
	}

	/// <summary>Runs an action that answers with a <see cref="BackendReply{T}"/>.</summary>
	/// <typeparam name="T">The success contract of the reply.</typeparam>
	/// <param name="call">The call to run.</param>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The reply's value, or the problem to show: the reply's own for a refused action, the transport's for no answer.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="call"/> is <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
	public static async Task<BackendResult<T>> RunReplyAsync<T>(Func<CancellationToken, Task<BackendReply<T>>> call, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(call);
		try
		{
			var reply = await call(cancellationToken).ConfigureAwait(false);
			return reply.IsSuccess ? new BackendResult<T>(reply.Value, null) : new BackendResult<T>(default, BackendProblem.FromReply(reply));
		}
		catch (Exception exception) when (BackendProblem.Handles(exception, cancellationToken))
		{
			return new BackendResult<T>(default, BackendProblem.FromException(exception));
		}
	}
	#endregion
}
