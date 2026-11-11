using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stores dates and times in UTC. Npgsql refuses a <see cref="DateTimeOffset"/> with another offset
///              for a <c>timestamptz</c> column, and values read back are in UTC; the instant is unchanged.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
	#region Constructors
	/// <summary>
	/// Initialises a new converter.
	/// </summary>
	public UtcDateTimeOffsetConverter()
		: base(value => value.ToUniversalTime(), value => value)
	{
	}
	#endregion
}
