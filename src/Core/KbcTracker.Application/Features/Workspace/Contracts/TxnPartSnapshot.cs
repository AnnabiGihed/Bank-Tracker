using KbcTracker.Domain.Features.Transactions.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers one part of a split payment to the web app, so its rebuilt analysis totals by category
///              without the stored decisions.
/// </summary>
/// <param name="CategoryId">The category of the part.</param>
/// <param name="Amount">The amount of the part, signed like the payment.</param>
/// <param name="Label">What the part is, or <see langword="null"/>.</param>
public sealed record TxnPartSnapshot(string CategoryId, decimal Amount, string? Label)
{
	#region Public Methods
	/// <summary>Copies a part of the central analysis.</summary>
	/// <param name="part">The part.</param>
	/// <returns>The client-safe snapshot.</returns>
	public static TxnPartSnapshot From(TxnPart part) => new(part.Category.Id.Value, part.Amount, part.Label);

	/// <summary>Rebuilds the part against the snapshot's category definitions.</summary>
	/// <param name="categories">The snapshot definitions.</param>
	/// <returns>The display part.</returns>
	public TxnPart ToDomain(IReadOnlyDictionary<string, Category> categories) => new(categories[CategoryId], Amount, Label);
	#endregion
}
