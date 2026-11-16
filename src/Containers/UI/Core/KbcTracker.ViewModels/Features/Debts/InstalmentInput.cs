namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the owner gives to size the instalments of a plan: the amount of each one, or how many there are.
///              The other is derived.
/// </summary>
public enum InstalmentInput
{
	/// <summary>The amount each time; the number of payments follows.</summary>
	AmountEach,

	/// <summary>The number of payments; the amount each time follows.</summary>
	Count,
}
