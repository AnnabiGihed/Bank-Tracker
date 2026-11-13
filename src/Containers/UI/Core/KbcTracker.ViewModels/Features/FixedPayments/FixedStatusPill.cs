namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The status of a fixed payment in a month as the pill shows it: the words, their colour and, when the
///              paid amount differs, what the payment usually costs.
/// </summary>
/// <param name="Status">The status in the month.</param>
/// <param name="Text">The words in the pill.</param>
/// <param name="Tooltip">More detail, or <see langword="null"/> for none.</param>
/// <param name="Tone">How the pill is coloured.</param>
public sealed record FixedStatusPill(FixedStatus Status, string Text, string? Tooltip, PillTone Tone)
{
	#region Factory Methods
	/// <summary>Words the status of a fixed payment in a month.</summary>
	/// <param name="payment">The fixed payment, whose usual amount is shown when the paid amount differs.</param>
	/// <param name="state">The status in the month, the amount paid and the date paid or expected.</param>
	/// <param name="formats">The formats of amounts and dates.</param>
	/// <returns>The pill.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="payment"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static FixedStatusPill Of(FixedRow payment, (FixedStatus Status, decimal Paid, DateOnly? Date) state, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(payment);
		ArgumentNullException.ThrowIfNull(formats);
		return state.Status switch
		{
			FixedStatus.Paid => new(state.Status, $"paid {formats.Day(state.Date)}", null, PillTone.Ok),
			FixedStatus.PaidChanged => new(state.Status, $"paid {formats.Money(state.Paid)} (usually {formats.WholeMoney(payment.Def.Amount)})",
				$"Usually {formats.Money(payment.Def.Amount)}", PillTone.Bad),
			FixedStatus.Due => new(state.Status, $"due ~{formats.Day(state.Date)}", null, PillTone.Due),
			FixedStatus.Late => new(state.Status, $"expected ~{formats.Day(state.Date)}, not seen", null, PillTone.Bad),
			FixedStatus.NotThisMonth => new(state.Status, "not this month", null, PillTone.Nil),
			_ => new(state.Status, "—", null, PillTone.Nil),
		};
	}
	#endregion
}
