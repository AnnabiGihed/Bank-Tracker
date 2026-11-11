using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Shared.Services;
using Pivot.Framework.Application.Abstractions.Messaging.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Computes the workspace on the backend where all bank source fields remain available.
/// </summary>
/// <param name="persistence">The consistent workspace reader.</param>
/// <param name="clock">The reference date provider.</param>
internal sealed class GetWorkspaceQueryHandler(IWorkspacePersistence persistence, TimeProvider clock) : IQueryHandler<GetWorkspaceQuery, WorkspaceSnapshot>
{
	#region Public Methods
	/// <inheritdoc />
	public async Task<Result<WorkspaceSnapshot>> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var data = await persistence.ReadAsync(cancellationToken).ConfigureAwait(false);
		var today = DateOnly.FromDateTime(clock.GetLocalNow().Date);
		var analysis = new Analysis(data.Transactions, data.Categories, data.User, today);
		var session = data.Session;
		return Result.Success(new WorkspaceSnapshot(AnalysisSnapshot.From(analysis, today),
			new ConnectionStatus(session is not null, session?.ValidUntil, session?.LastSync, session?.SkippedTransactions ?? 0, session?.LastError is not null),
			session?.AccountUids.Count ?? 0));
	}
	#endregion
}
