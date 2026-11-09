namespace KbcTracker.Domain.Features.Categories.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The category of every payment of one payee series, as chosen or confirmed by the user.
/// </summary>
public sealed class PayeeAssignment
{
	#region Properties
	/// <summary>
	/// Gets or sets the category id, or <see langword="null"/> to fall back to the automatic category.
	/// </summary>
	public string? CategoryId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the user confirmed the category (it then leaves the review queue).
	/// </summary>
	public bool Confirmed { get; set; }
	#endregion
}
