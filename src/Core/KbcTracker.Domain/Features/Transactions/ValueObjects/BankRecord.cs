namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the bank sent about a transaction beyond its core values: the domain of the ISO bank
///              transaction code and the original JSON. The analysis reads the full code and the names from the
///              JSON, so it is kept unchanged.
/// </summary>
/// <param name="BankCode">The domain of the ISO bank transaction code, for example <c>PMNT</c>, when sent.</param>
/// <param name="Raw">The original JSON from the bank, when available.</param>
public sealed record BankRecord(string? BankCode, string? Raw)
{
	#region Static Instances
	/// <summary>
	/// A transaction without any bank record (data created before the raw JSON was kept).
	/// </summary>
	public static readonly BankRecord None = new(null, null);
	#endregion
}
