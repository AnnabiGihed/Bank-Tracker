namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Tells what a payment's fixed-payment cell shows.
/// </summary>
public enum FixedMark
{
	/// <summary>Nothing: money in and own transfers are never fixed payments.</summary>
	None,

	/// <summary>A star that opens the fixed payment the payment belongs to.</summary>
	IsFixed,

	/// <summary>A button that marks the payee as a fixed payment.</summary>
	CanMark,
}
