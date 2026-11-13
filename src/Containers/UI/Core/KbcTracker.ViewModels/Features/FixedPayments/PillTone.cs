namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Tells how a status pill is coloured, so a component maps it to a style once.
/// </summary>
public enum PillTone
{
	/// <summary>Paid as expected (green).</summary>
	Ok,

	/// <summary>Still to come (orange).</summary>
	Due,

	/// <summary>Paid with another amount, or late (red).</summary>
	Bad,

	/// <summary>Nothing to report (grey).</summary>
	Nil,
}
