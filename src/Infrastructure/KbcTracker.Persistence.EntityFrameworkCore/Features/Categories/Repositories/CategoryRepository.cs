using KbcTracker.Domain.Features.Categories.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Categories.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Implements ICategoryRepository over the scoped PostgreSQL context.
/// </summary>
/// <param name="dbContext">The scoped context whose changes the unit of work commits.</param>
internal sealed class CategoryRepository(KbcTrackerDbContext dbContext) : ICategoryRepository
{
	#region ICategoryRepository Implementation
	/// <inheritdoc />
	public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
		await dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id)
			.ToListAsync(cancellationToken).ConfigureAwait(false);
	#endregion
}
