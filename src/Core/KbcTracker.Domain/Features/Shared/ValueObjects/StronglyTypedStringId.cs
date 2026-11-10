namespace KbcTracker.Domain.Features.Shared.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Base of the strongly typed identifiers whose value is a string: ids the bank assigns (transactions)
///              and ids the user can read (category slugs). Mirrors Pivot.Framework's <c>StronglyTypedGuidId</c>:
///              the value is required, and equality, ordering and hashing use it, compared ordinally.
/// </summary>
/// <typeparam name="TSelf">The concrete identifier type.</typeparam>
public abstract record StronglyTypedStringId<TSelf> : IStronglyTypedId<TSelf>
	where TSelf : StronglyTypedStringId<TSelf>
{
	#region Properties
	/// <summary>
	/// Gets the identifier value.
	/// </summary>
	public string Value { get; }
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new identifier.
	/// </summary>
	/// <param name="value">The identifier value.</param>
	/// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or white space.</exception>
	protected StronglyTypedStringId(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Value = value;
	}
	#endregion

	#region Equality
	/// <summary>
	/// Determines whether another identifier of the same type has the same value (ordinal comparison).
	/// </summary>
	/// <param name="other">The other identifier.</param>
	/// <returns><see langword="true"/> when both values are equal.</returns>
	public bool Equals(TSelf? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

	/// <inheritdoc />
	public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

	/// <summary>
	/// Compares the value with another identifier's value, ordinally.
	/// </summary>
	/// <param name="other">The other identifier.</param>
	/// <returns>A negative number, zero or a positive number; positive when <paramref name="other"/> is <see langword="null"/>.</returns>
	public int CompareTo(TSelf? other) => other is null ? 1 : string.CompareOrdinal(Value, other.Value);
	#endregion

	#region Operators
	/// <summary>
	/// Determines whether the left identifier sorts before the right one.
	/// </summary>
	/// <param name="left">The left identifier.</param>
	/// <param name="right">The right identifier.</param>
	/// <returns><see langword="true"/> when <paramref name="left"/> sorts first; <see langword="null"/> sorts first.</returns>
	public static bool operator <(StronglyTypedStringId<TSelf>? left, StronglyTypedStringId<TSelf>? right) => Compare(left, right) < 0;

	/// <summary>
	/// Determines whether the left identifier sorts before the right one or equals it.
	/// </summary>
	/// <param name="left">The left identifier.</param>
	/// <param name="right">The right identifier.</param>
	/// <returns><see langword="true"/> unless <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
	public static bool operator <=(StronglyTypedStringId<TSelf>? left, StronglyTypedStringId<TSelf>? right) => Compare(left, right) <= 0;

	/// <summary>
	/// Determines whether the left identifier sorts after the right one.
	/// </summary>
	/// <param name="left">The left identifier.</param>
	/// <param name="right">The right identifier.</param>
	/// <returns><see langword="true"/> when <paramref name="left"/> sorts last; <see langword="null"/> sorts first.</returns>
	public static bool operator >(StronglyTypedStringId<TSelf>? left, StronglyTypedStringId<TSelf>? right) => Compare(left, right) > 0;

	/// <summary>
	/// Determines whether the left identifier sorts after the right one or equals it.
	/// </summary>
	/// <param name="left">The left identifier.</param>
	/// <param name="right">The right identifier.</param>
	/// <returns><see langword="true"/> unless <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
	public static bool operator >=(StronglyTypedStringId<TSelf>? left, StronglyTypedStringId<TSelf>? right) => Compare(left, right) >= 0;
	#endregion

	#region Overrides
	/// <inheritdoc />
	public override string ToString() => Value;
	#endregion

	#region Private Helpers
	/// <summary>
	/// Compares two identifier values ordinally; <see langword="null"/> sorts first.
	/// </summary>
	private static int Compare(StronglyTypedStringId<TSelf>? left, StronglyTypedStringId<TSelf>? right) =>
		string.CompareOrdinal(left?.Value, right?.Value);
	#endregion
}
