using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.Shared;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using Pivot.Framework.Domain.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Commits new bank credentials through Pivot, preserving the prior successful sync date and history.
/// </summary>
/// <param name="dbContext">The scoped central database context.</param>
/// <param name="sessions">The current consent repository.</param>
/// <param name="unitOfWork">Commits the replacement atomically.</param>
/// <param name="clock">The connection timestamp source.</param>
/// <param name="settings">The provider consent lifetime.</param>
public sealed class PostgreSqlBankConsentPersistence(KbcTrackerDbContext dbContext, IBankSessionRepository sessions,
	IUnitOfWork unitOfWork, TimeProvider clock, BankConsentSettings settings) : IBankConsentPersistence
{
	#region IBankConsentPersistence Implementation
	/// <inheritdoc />
	public async Task<Result<ConnectionStatus>> SaveAsync(BankConsentSession session, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(session);
		if (!await dbContext.UserData.AnyAsync(cancellationToken).ConfigureAwait(false))
		{
			// A saved connection would make the one-time import of the JSON archive impossible.
			return Result.Failure<ConnectionStatus>(BackendErrors.NotInitialized, ResultExceptionType.Conflict);
		}

		var current = await sessions.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
		var now = clock.GetUtcNow();
		var replacement = SessionInfo.Connect(new BankSessionId(session.SessionId), session.AccountUids, now,
			now.AddDays(settings.ConsentDays), current?.LastSync);
		if (replacement.IsFailure)
		{
			return Result.Failure<ConnectionStatus>(replacement.Error);
		}

		var existing = await dbContext.BankSessions.FindAsync([replacement.Value.Id], cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			dbContext.BankSessions.Add(replacement.Value);
		}
		else
		{
			dbContext.Entry(existing).CurrentValues.SetValues(replacement.Value);
			dbContext.Entry(existing).Property<List<string>>("_accountUids").CurrentValue = session.AccountUids.ToList();
		}

		var saved = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return saved.IsSuccess
			? Result.Success(new ConnectionStatus(true, replacement.Value.ValidUntil, replacement.Value.LastSync, 0, false))
			: Result.Failure<ConnectionStatus>(saved.Error, saved.ResultExceptionType);
	}
	#endregion
}
