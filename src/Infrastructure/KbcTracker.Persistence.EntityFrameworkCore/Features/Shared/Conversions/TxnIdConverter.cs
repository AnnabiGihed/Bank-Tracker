namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a <see cref="TxnId"/> as its string value.
/// </summary>
public sealed class TxnIdConverter : StringIdConverter<TxnId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public TxnIdConverter()
		: base(value => new TxnId(value))
	{
	}
	#endregion
}
