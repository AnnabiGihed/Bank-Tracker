namespace KbcTracker.Domain.Features.FixedPayments.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A fixed payment the user accepted. New transactions are matched to it automatically, by payee
///              series or by shop words and amount, so a changed bank label does not break it.
///
///              Created only from a user action (accepting a suggestion or marking a payee), never automatically.
/// </summary>
public sealed class RecurringPayment
{
	#region Properties
	/// <summary>
	/// Gets or sets the id: the first 10 characters of a new GUID.
	/// </summary>
	public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];

	/// <summary>
	/// Gets or sets the name shown to the user.
	/// </summary>
	public string Name { get; set; } = "";

	/// <summary>
	/// Gets or sets the category of its payments, or <see langword="null"/> to use the payments' own category.
	/// </summary>
	public string? CategoryId { get; set; }

	/// <summary>
	/// Gets or sets the payee series it covers (direct-debit mandate, IBAN, shop…).
	/// </summary>
	public IList<string> SeriesKeys { get; set; } = [];

	/// <summary>
	/// Gets or sets shop-name words that identify it even when the bank label changes (for example GOOGLE YOUTUBE).
	/// </summary>
	public IList<string> Tokens { get; set; } = [];

	/// <summary>
	/// Gets or sets the usual amount, as a positive number.
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Gets or sets the allowed difference with <see cref="Amount"/> for matching by shop words, in percent
	/// (energy bills vary more than a streaming subscription).
	/// </summary>
	public decimal TolerancePct { get; set; } = 15;

	/// <summary>
	/// Gets or sets the number of days between payments: 30 monthly, 91 quarterly, 365 yearly.
	/// </summary>
	public int CadenceDays { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether new payments are still matched to it.
	/// </summary>
	public bool Active { get; set; } = true;

	/// <summary>
	/// Gets or sets when the user accepted it (UTC).
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
	#endregion
}
