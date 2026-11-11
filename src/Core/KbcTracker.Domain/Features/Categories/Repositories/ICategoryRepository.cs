using KbcTracker.Domain.Features.Categories.Aggregates;

namespace KbcTracker.Domain.Features.Categories.Repositories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reads the centrally stored category definitions for both clients.
/// </summary>
public interface ICategoryRepository
{
	#region Methods
	/// <summary>Reads the stored category definitions without creating defaults.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The categories ordered by name.</returns>
	Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
	#endregion
}
