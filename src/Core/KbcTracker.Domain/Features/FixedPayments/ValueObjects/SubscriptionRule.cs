namespace KbcTracker.Domain.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The rule that tells a subscription from another fixed payment: a recurring payment whose payments are
///              all card payments and that repeats monthly or yearly. It is derived from the recurring-payment
///              detection, with no list of brands, and changes no total: a subscription is still a fixed payment.
///
///              A series that mixes card payments with anything else (a transfer, a direct debit) is not a
///              subscription: the owner paid it in a way a subscription is not, so it stays on the Fixed payments tab.
///              A recurring payment with no payment matched yet cannot be told and is not a subscription either.
/// </summary>
public static class SubscriptionRule
{
	#region Public Methods
	/// <summary>Tells whether a recurring payment is a subscription.</summary>
	/// <param name="cadenceDays">Its rhythm in days; only monthly and yearly (see <see cref="Analysis.CadenceName"/>) count.</param>
	/// <param name="payments">Its payments.</param>
	/// <returns><see langword="true"/> when there is at least one payment, every payment is a card payment and the rhythm is monthly or yearly.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="payments"/> is <see langword="null"/>.</exception>
	public static bool IsSubscription(int cadenceDays, IReadOnlyCollection<TxnInfo> payments)
	{
		ArgumentNullException.ThrowIfNull(payments);
		return payments.Count > 0
			&& payments.All(p => p.Kind == Kind.Card)
			&& Analysis.CadenceName(cadenceDays) is "Monthly" or "Yearly";
	}
	#endregion
}
