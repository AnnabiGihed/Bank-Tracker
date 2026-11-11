using KbcTracker.Domain.Features.BankConnection.Repositories;
using Pivot.Framework.Application.Abstractions.Messaging.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Projects the latest consent without exposing its credentials or bank error payload.
/// </summary>
/// <param name="sessions">The central bank consents.</param>
internal sealed class GetConnectionStatusQueryHandler(IBankSessionRepository sessions) : IQueryHandler<GetConnectionStatusQuery, ConnectionStatus>
{
	#region IQueryHandler Implementation
	/// <inheritdoc />
	public async Task<Result<ConnectionStatus>> Handle(GetConnectionStatusQuery request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var session = await sessions.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
		return Result.Success(new ConnectionStatus(session is not null, session?.ValidUntil, session?.LastSync,
			session?.SkippedTransactions ?? 0, session?.LastError is not null));
	}
	#endregion
}
