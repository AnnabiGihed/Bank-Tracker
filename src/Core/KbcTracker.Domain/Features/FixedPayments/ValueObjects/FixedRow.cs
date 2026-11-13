using System.Globalization;

namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : An accepted fixed payment with the transactions matched to it, able to tell its state in any
///              month (paid, changed amount, due, late…).
/// </summary>
/// <param name="Def">The accepted fixed payment.</param>
/// <param name="Category">Its category.</param>
/// <param name="Payments">The transactions matched to it, oldest first.</param>
public sealed record FixedRow(RecurringPayment Def, Category Category, IReadOnlyList<TxnInfo> Payments)
{
	#region Properties
	/// <summary>
	/// Gets the date of the latest payment, or <see langword="null"/> when none was matched yet.
	/// </summary>
	public DateOnly? LastPaid => Payments.Count == 0 ? null : Payments.Max(p => p.T.Date);

	/// <summary>
	/// Gets the median day of the month it is paid on; 1 when no payment was matched yet.
	/// </summary>
	public int UsualDay => Payments.Count == 0 ? 1 : Payments.Select(p => p.T.Date.Day).Order().ElementAt(Payments.Count / 2);

	/// <summary>
	/// Gets a value indicating whether it is a subscription: paid by card only, monthly or yearly (see <see cref="SubscriptionRule"/>).
	/// </summary>
	public bool IsSubscription => SubscriptionRule.IsSubscription(Def.CadenceDays, Payments.ToList());
	#endregion

	#region Public Methods
	/// <summary>
	/// Determines the state of the fixed payment in a month. Expected dates are projected from the last payment
	/// with the cadence; when none falls in the month, the usual day is used.
	/// </summary>
	/// <param name="month">The month, as <c>yyyy-MM</c>.</param>
	/// <param name="today">The current date, which separates due from late.</param>
	/// <returns>The state, the amount paid in the month (0 when unpaid), and the latest payment date when paid or
	/// the expected date when unpaid (<see langword="null"/> for <see cref="FixedStatus.NotStarted"/> and for
	/// <see cref="FixedStatus.NotThisMonth"/> of a quarterly or yearly payment).</returns>
	public (FixedStatus Status, decimal Paid, DateOnly? Date) StatusIn(string month, DateOnly today)
	{
		var inMonth = Payments.Where(p => string.Equals(p.Month, month, StringComparison.Ordinal)).ToList();
		if (inMonth.Count > 0)
		{
			var paid = inMonth.Sum(p => -p.T.Amount);
			var difference = Math.Abs(paid - Def.Amount);
			var changed = difference >= 1 && difference > Def.Amount * 0.02m;
			return (changed ? FixedStatus.PaidChanged : FixedStatus.Paid, paid, inMonth.Max(p => p.T.Date));
		}

		var first = Payments.Count == 0 ? DateOnly.FromDateTime(Def.CreatedAt.LocalDateTime) : Payments.Min(p => p.T.Date);
		if (string.CompareOrdinal(month, Analysis.MonthOf(first)) < 0)
		{
			return (FixedStatus.NotStarted, 0, null);
		}

		var last = LastPaid ?? first;
		var expected = Enumerable.Range(1, 24).Select(k => last.AddDays(k * Def.CadenceDays))
			.Where(d => string.Equals(Analysis.MonthOf(d), month, StringComparison.Ordinal)).ToList();
		if (expected.Count == 0 && Def.CadenceDays > 40)
		{
			return (FixedStatus.NotThisMonth, 0, null);
		}

		var usualDate = new DateOnly(
			int.Parse(month[..4], CultureInfo.InvariantCulture), int.Parse(month[5..], CultureInfo.InvariantCulture), Math.Min(UsualDay, 28));
		var expectedDate = expected.FirstOrDefault(usualDate);
		if (string.CompareOrdinal(month, Analysis.MonthOf(today)) < 0)
		{
			return (FixedStatus.NotThisMonth, 0, expectedDate);
		}

		return (today <= expectedDate.AddDays(4) ? FixedStatus.Due : FixedStatus.Late, 0, expectedDate);
	}
	#endregion
}
