namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How new transactions are recognised as a fixed payment: by payee series (direct-debit mandate,
///              IBAN, shop…) or by the shop words that survive a changed bank label (for example GOOGLE YOUTUBE).
/// </summary>
public sealed record PayeeMatch
{
	#region Properties
	/// <summary>
	/// Gets the payee series it covers.
	/// </summary>
	public IReadOnlyList<string> SeriesKeys { get; }

	/// <summary>
	/// Gets the shop words that identify it even if the bank label changes.
	/// </summary>
	public IReadOnlyList<string> Tokens { get; }

	/// <summary>
	/// Gets the signature stored when the user dismisses or stops tracking it: the distinct series keys in ordinal
	/// order, joined with <c>|</c>.
	/// </summary>
	public string Signature => SignatureOf(SeriesKeys);
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="PayeeMatch"/>.
	/// </summary>
	/// <param name="seriesKeys">The payee series it covers.</param>
	/// <param name="tokens">The shop words that identify it; empty when it matches by series only.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public PayeeMatch(IEnumerable<string> seriesKeys, IEnumerable<string> tokens)
	{
		ArgumentNullException.ThrowIfNull(seriesKeys);
		ArgumentNullException.ThrowIfNull(tokens);
		SeriesKeys = [.. seriesKeys];
		Tokens = [.. tokens];
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Builds the signature of a set of series keys: the distinct keys in ordinal order, joined with <c>|</c>.
	/// Every place that stores or compares a signature uses this.
	/// </summary>
	/// <param name="seriesKeys">The series keys.</param>
	/// <returns>The signature.</returns>
	public static string SignatureOf(IEnumerable<string> seriesKeys) =>
		string.Join('|', seriesKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
	#endregion

	#region Equality
	/// <summary>
	/// Determines whether another match covers the same series keys and shop words, in the same order (ordinal).
	/// Records would otherwise compare the lists by reference.
	/// </summary>
	/// <param name="other">The other match.</param>
	/// <returns><see langword="true"/> when both hold the same keys and words.</returns>
	public bool Equals(PayeeMatch? other) =>
		other is not null
		&& SeriesKeys.SequenceEqual(other.SeriesKeys, StringComparer.Ordinal)
		&& Tokens.SequenceEqual(other.Tokens, StringComparer.Ordinal);

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var key in SeriesKeys)
		{
			hash.Add(key, StringComparer.Ordinal);
		}

		hash.Add('|');
		foreach (var token in Tokens)
		{
			hash.Add(token, StringComparer.Ordinal);
		}

		return hash.ToHashCode();
	}
	#endregion
}
