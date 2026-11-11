using KbcTracker.Domain.Features.Categories.Repositories;
using Pivot.Framework.Application.Abstractions.Messaging.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Categories.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps stored categories to the public read contract without seeding on reads.
/// </summary>
/// <param name="categories">The central category definitions.</param>
internal sealed class ListCategoriesQueryHandler(ICategoryRepository categories)
	: IQueryHandler<ListCategoriesQuery, IReadOnlyList<CategorySummary>>
{
	#region IQueryHandler Implementation
	/// <inheritdoc />
	public async Task<Result<IReadOnlyList<CategorySummary>>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var stored = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
		IReadOnlyList<CategorySummary> result = [.. stored.Select(c => new CategorySummary(c.Id.Value, c.Name, c.Type.ToString(), c.Color, [.. c.Keywords]))];
		return Result.Success(result);
	}
	#endregion
}
