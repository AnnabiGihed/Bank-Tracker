using KbcTracker.Application.Features.BankConnection.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Persists a new consent without exposing database tracking or credentials to controllers.
/// </summary>
public interface IBankConsentPersistence
{
	#region Methods
	/// <summary>Replaces the current consent, preserving history and the previous successful sync date.</summary>
	/// <param name="session">The backend-only provider credentials.</param>
	/// <param name="cancellationToken">Cancels persistence.</param>
	/// <returns>The safe connection status, or a persistence failure.</returns>
	Task<Result<ConnectionStatus>> SaveAsync(BankConsentSession session, CancellationToken cancellationToken);
	#endregion
}
