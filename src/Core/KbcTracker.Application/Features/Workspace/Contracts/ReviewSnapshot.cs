using KbcTracker.Domain.Features.Review.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a payee awaiting category confirmation.
/// </summary>
/// <param name="SeriesKey">The existing payee key.</param>
/// <param name="Name">The display name.</param>
/// <param name="CategoryId">The automatic category.</param>
/// <param name="Count">The unconfirmed transaction count.</param>
/// <param name="Total">The signed total.</param>
/// <param name="Last">The latest booking date.</param>
/// <param name="Incoming">Whether the latest movement is income.</param>
/// <param name="Sample">The bank-provided description.</param>
public sealed record ReviewSnapshot(string SeriesKey, string Name, string CategoryId, int Count, decimal Total, DateOnly Last, bool Incoming, string Sample)
{
	#region Public Methods
	/// <summary>Copies a central review item.</summary>
	/// <param name="item">The computed item.</param>
	/// <returns>The snapshot.</returns>
	public static ReviewSnapshot From(ReviewItem item) => new(item.SeriesKey, item.Name, item.Category.Id.Value,
		item.Count, item.Total, item.Last, item.Incoming, item.Sample);

	/// <summary>Restores the item with its shared category instance.</summary>
	/// <param name="categories">The definitions in the snapshot.</param>
	/// <returns>The display review item.</returns>
	public ReviewItem ToDomain(IReadOnlyDictionary<string, Category> categories) => new(SeriesKey, Name,
		categories[CategoryId], Count, Total, Last, Incoming, Sample);
	#endregion
}
