using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.BankConnection.Services;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Completes bank callbacks through the central consent workflow, then syncs the whole history the
///              bank still holds.
///
///              The full sync is what keeps a reconnect from losing history: after a consent gap, the daily
///              incremental sync asks for only 30 days, so the older part of the bank's 90-day window would never be
///              fetched and would be gone once it left that window. It runs after the workflow released its sync
///              lease; its outcome is recorded on the new connection, so a failed sync never undoes the consent.
/// </summary>
/// <param name="workflow">The central consent workflow.</param>
/// <param name="sync">Runs the full sync after the consent is saved.</param>
/// <param name="sessions">Reads the connection as the sync left it.</param>
internal sealed class CompleteBankConsentCommandHandler(BankConsentWorkflow workflow, SyncService sync, IBankSessionRepository sessions)
	: ICommandHandler<CompleteBankConsentCommand, ConnectionStatus>
{
	#region ICommandHandler Implementation
	/// <inheritdoc />
	public async Task<Result<ConnectionStatus>> Handle(CompleteBankConsentCommand request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var completed = await workflow.CompleteAsync(request.Code, request.State, request.Error, request.Owner, cancellationToken).ConfigureAwait(false);
		if (completed.IsFailure)
		{
			return completed;
		}

		await sync.SyncAsync(full: true, cancellationToken).ConfigureAwait(false);
		var session = await sessions.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
		return session is null
			? completed
			: Result.Success(new ConnectionStatus(true, session.ValidUntil, session.LastSync, session.SkippedTransactions, session.LastError is not null));
	}
	#endregion
}
