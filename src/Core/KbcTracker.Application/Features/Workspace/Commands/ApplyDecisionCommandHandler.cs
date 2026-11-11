using KbcTracker.Application.Features.Shared;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Serialises central UI decisions and commits category changes with user decisions atomically.
/// </summary>
/// <param name="persistence">The scoped workspace unit of work.</param>
/// <param name="clock">The time assigned to accepted fixed payments.</param>
internal sealed class ApplyDecisionCommandHandler(IWorkspacePersistence persistence, TimeProvider clock)
	: ICommandHandler<ApplyDecisionCommand, DecisionResponse>
{
	#region Public Methods
	/// <inheritdoc />
	public async Task<Result<DecisionResponse>> Handle(ApplyDecisionCommand request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (request.Decision is null)
		{
			return Result.Failure<DecisionResponse>(new Error("Workspace.Decision.Required", "A decision is required."));
		}

		var lease = await persistence.TryAcquireWriteAsync(cancellationToken).ConfigureAwait(false);
		if (lease is null)
		{
			return Conflict();
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
	/// <summary>Returns a safe retryable failure without exposing persistence details.</summary>
	private static Result<DecisionResponse> Conflict() => Result.Failure<DecisionResponse>(
		new Error("Workspace.Conflict", "The decision could not be saved. Refresh the page and try again."), ResultExceptionType.Conflict);
	#endregion
}
