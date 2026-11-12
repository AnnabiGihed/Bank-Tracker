using KbcTracker.Application.Features.Shared;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Serialises central UI decisions and commits category changes with user decisions atomically. A decision
///              that finds another one being saved waits briefly for it, so a double-click or two quick edits both land.
/// </summary>
/// <param name="persistence">The scoped workspace unit of work.</param>
/// <param name="clock">The time assigned to accepted fixed payments, and the clock the wait for the write lease runs on.</param>
internal sealed class ApplyDecisionCommandHandler(IWorkspacePersistence persistence, TimeProvider clock)
	: ICommandHandler<ApplyDecisionCommand, DecisionResponse>
{
	#region Static Instances
	/// <summary>How long a decision waits for the one being saved: a save takes milliseconds, so only a stuck writer
	/// exhausts it, and the page still answers within a few seconds.</summary>
	internal static readonly TimeSpan WriteWaitLimit = TimeSpan.FromSeconds(5);

	/// <summary>The pause between two attempts to take the write lease; short enough that a waiting decision follows the
	/// previous save almost at once, long enough not to flood the database with lock attempts.</summary>
	internal static readonly TimeSpan WriteRetryInterval = TimeSpan.FromMilliseconds(100);
	#endregion

	#region Public Methods
	/// <inheritdoc />
	/// <remarks>Fails with <c>Workspace.Busy</c> (<see cref="ResultExceptionType.Conflict"/>) when another decision still
	/// holds the write lease after <see cref="WriteWaitLimit"/>, and with <c>Workspace.Conflict</c> when the save fails.</remarks>
	public async Task<Result<DecisionResponse>> Handle(ApplyDecisionCommand request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (request.Decision is null)
		{
			return Result.Failure<DecisionResponse>(new Error("Workspace.Decision.Required", "A decision is required."));
		}

		var lease = await WaitForWriteLeaseAsync(cancellationToken).ConfigureAwait(false);
		if (lease is null)
		{
			return Busy();
		}

		await using (lease.ConfigureAwait(false))
		{
			var data = await persistence.LoadForUpdateAsync(cancellationToken).ConfigureAwait(false);
			if (data is null)
			{
				return Result.Failure<DecisionResponse>(BackendErrors.NotInitialized, ResultExceptionType.Conflict);
			}

			var result = new WorkspaceDecisions(data, persistence, clock).Apply(request.Decision);
			if (result.IsFailure)
			{
				return result;
			}

			var saved = await persistence.SaveAsync(cancellationToken).ConfigureAwait(false);
			return saved.IsSuccess ? result : Conflict();
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Takes the write lease, retrying while another decision holds it, for at most <see cref="WriteWaitLimit"/>.</summary>
	/// <remarks>Polling the non-blocking lock keeps the lease on the persistence's own connection and never leaves a lock
	/// request pending in the database when the request is cancelled, unlike a blocking lock under a timeout.</remarks>
	/// <param name="cancellationToken">Cancels the wait; the cancellation propagates as an exception.</param>
	/// <returns>The lease, or <see langword="null"/> when another writer still holds it once the limit has passed.</returns>
	private async Task<IAsyncDisposable?> WaitForWriteLeaseAsync(CancellationToken cancellationToken)
	{
		var started = clock.GetTimestamp();
		var lease = await persistence.TryAcquireWriteAsync(cancellationToken).ConfigureAwait(false);
		while (lease is null && clock.GetElapsedTime(started) < WriteWaitLimit)
		{
			await Task.Delay(WriteRetryInterval, clock, cancellationToken).ConfigureAwait(false);
			lease = await persistence.TryAcquireWriteAsync(cancellationToken).ConfigureAwait(false);
		}

		return lease;
	}

	/// <summary>Returns the retryable failure for a decision that waited too long for another one being saved.</summary>
	private static Result<DecisionResponse> Busy() => Result.Failure<DecisionResponse>(
		new Error("Workspace.Busy", "Another change is still being saved. Try again in a moment."), ResultExceptionType.Conflict);

	/// <summary>Returns a safe retryable failure without exposing persistence details.</summary>
	private static Result<DecisionResponse> Conflict() => Result.Failure<DecisionResponse>(
		new Error("Workspace.Conflict", "The decision could not be saved. Refresh the page and try again."), ResultExceptionType.Conflict);
	#endregion
}
