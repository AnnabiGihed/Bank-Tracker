using KbcTracker.Application.Features.BankConnection.Services;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Triggers the central sync and keeps internal provider and filesystem errors out of the API.
/// </summary>
/// <param name="sync">The scoped application sync workflow.</param>
internal sealed class SyncBankCommandHandler(SyncService sync) : ICommandHandler<SyncBankCommand, SyncSummary>
{
	#region ICommandHandler Implementation
	/// <inheritdoc />
	public async Task<Result<SyncSummary>> Handle(SyncBankCommand request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var (added, error) = await sync.SyncAsync(request.Full, cancellationToken).ConfigureAwait(false);
		return error is null
			? Result.Success(new SyncSummary(added))
			: Result.Failure<SyncSummary>(new Error("BankSync.Unavailable", "The bank sync could not complete. Check the backend logs."), ResultExceptionType.Conflict);
	}
	#endregion
}
