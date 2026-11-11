namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores a <see cref="RecurringPaymentId"/> as its string value.
/// </summary>
public sealed class RecurringPaymentIdConverter : StringIdConverter<RecurringPaymentId>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public RecurringPaymentIdConverter()
		: base(value => new RecurringPaymentId(value))
	{
	}
	#endregion
}
