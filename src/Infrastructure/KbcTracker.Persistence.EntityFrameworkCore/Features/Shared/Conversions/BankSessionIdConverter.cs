namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a <see cref="BankSessionId"/> as its string value.
/// </summary>
public sealed class BankSessionIdConverter : StringIdConverter<BankSessionId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public BankSessionIdConverter()
		: base(value => new BankSessionId(value))
	{
	}
	#endregion
}
