using KbcTracker.Domain.Features.Shared.Aggregates;

namespace KbcTracker.Domain.Features.Shared.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Loads the central user decisions with their owned recurring payments.
/// </summary>
public interface IUserDataRepository
{
	#region Methods
	/// <summary>Loads the current decisions for use by a scoped unit of work.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The decisions, or null before initialisation/import.</returns>
	Task<UserData?> GetCurrentAsync(CancellationToken cancellationToken);
	#endregion
}
