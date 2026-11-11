using KbcTracker.Domain.Features.BankConnection.Aggregates;

namespace KbcTracker.Domain.Features.BankConnection.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Loads the latest consent and checks reconnects before saving a sync outcome.
/// </summary>
public interface IBankSessionRepository
{
	#region Methods
	/// <summary>Loads the most recently connected consent for use by a scoped unit of work.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The consent, or null when no connection exists.</returns>
	Task<SessionInfo?> GetCurrentAsync(CancellationToken cancellationToken);

	/// <summary>Checks the database's current consent independently of an earlier tracked snapshot.</summary>
	/// <param name="session">The consent used by the sync.</param>
	/// <param name="cancellationToken">Cancels the check.</param>
	/// <returns>True when both the consent id and connection time still match.</returns>
	Task<bool> IsCurrentAsync(SessionInfo session, CancellationToken cancellationToken);
	#endregion
}
