namespace KbcTracker.ViewModels.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Filters the payees to review by the direction of their money.
/// </summary>
public enum DirectionFilter
{
	/// <summary>Every payee.</summary>
	All,

	/// <summary>Only payees the owner pays (money out).</summary>
	Out,

	/// <summary>Only payees that pay the owner (money in).</summary>
	In,
}
