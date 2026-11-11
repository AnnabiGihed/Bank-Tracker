using KbcTracker.Domain.Features.Shared.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Implements IUserDataRepository over the scoped PostgreSQL context.
/// </summary>
/// <param name="dbContext">The scoped context whose changes the unit of work commits.</param>
internal sealed class UserDataRepository(KbcTrackerDbContext dbContext) : IUserDataRepository
{
	#region IUserDataRepository Implementation
	/// <inheritdoc />
	public Task<UserData?> GetCurrentAsync(CancellationToken cancellationToken) =>
		dbContext.UserData.SingleOrDefaultAsync(cancellationToken);
	#endregion
}
