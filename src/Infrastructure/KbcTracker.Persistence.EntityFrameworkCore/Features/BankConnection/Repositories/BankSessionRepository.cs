using KbcTracker.Domain.Features.BankConnection.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Implements IBankSessionRepository over the scoped PostgreSQL context.
/// </summary>
/// <param name="dbContext">The scoped context whose changes the unit of work commits.</param>
internal sealed class BankSessionRepository(KbcTrackerDbContext dbContext) : IBankSessionRepository
{
	#region IBankSessionRepository Implementation
	/// <inheritdoc />
	public Task<SessionInfo?> GetCurrentAsync(CancellationToken cancellationToken) => dbContext.BankSessions
		.OrderByDescending(s => s.ConnectedAt).ThenBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);

	/// <inheritdoc />
	public async Task<bool> IsCurrentAsync(SessionInfo session, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(session);
		var current = await dbContext.BankSessions.AsNoTracking().OrderByDescending(s => s.ConnectedAt).ThenBy(s => s.Id)
			.Select(s => new { s.Id, s.ConnectedAt }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return current is not null && current.Id == session.Id && current.ConnectedAt == session.ConnectedAt;
	}
	#endregion
}
