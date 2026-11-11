namespace KbcTracker.Infrastructure.Features.Legacy.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One accepted fixed payment as stored in <c>userdata.json</c>, in the file's existing shape; a
///              missing field takes the value older versions used.
/// </summary>
public sealed record RecurringPaymentDocument
{
	#region Properties
	/// <summary>
	/// Gets the fixed payment id.
	/// </summary>
	public string Id { get; init; } = "";

	/// <summary>
	/// Gets the name.
	/// </summary>
	public string Name { get; init; } = "";

	/// <summary>
	/// Gets the category id, if any.
	/// </summary>
	public string? CategoryId { get; init; }

	/// <summary>
	/// Gets the payee series it covers.
	/// </summary>
	public IReadOnlyList<string> SeriesKeys { get; init; } = [];

	/// <summary>
	/// Gets the shop words that identify it.
	/// </summary>
	public IReadOnlyList<string> Tokens { get; init; } = [];

	/// <summary>
	/// Gets the usual amount.
	/// </summary>
	public decimal Amount { get; init; }

	/// <summary>
	/// Gets the matching tolerance, in percent; 15, the value older versions used when it was missing.
	/// </summary>
	public decimal TolerancePct { get; init; } = 15;

	/// <summary>
	/// Gets the number of days between payments; monthly when it was missing, as in older versions.
	/// </summary>
	public int CadenceDays { get; init; } = 30;

	/// <summary>
	/// Gets a value indicating whether it is still matched.
	/// </summary>
	public bool Active { get; init; } = true;

	/// <summary>
	/// Gets when the user accepted it.
	/// </summary>
	public DateTimeOffset CreatedAt { get; init; }
	#endregion

	#region Public Methods
	/// <summary>
	/// Stores a fixed payment.
	/// </summary>
	/// <param name="payment">The fixed payment.</param>
	/// <returns>The document.</returns>
	public static RecurringPaymentDocument From(RecurringPayment payment) => new()
	{
		Id = payment.Id.Value,
		Name = payment.Name,
		CategoryId = payment.CategoryId?.Value,
		SeriesKeys = [.. payment.SeriesKeys],
		Tokens = [.. payment.Tokens],
		Amount = payment.Amount,
		TolerancePct = payment.TolerancePct,
		CadenceDays = payment.CadenceDays,
		Active = payment.Active,
		CreatedAt = payment.CreatedAt,
	};

	/// <summary>
	/// Rebuilds the stored fixed payment as it is. A blank id gets a stable id derived from its payees and name,
	/// and a blank category keeps its old meaning, "Other".
	/// </summary>
	/// <returns>The fixed payment.</returns>
	public RecurringPayment ToDomain() => RecurringPayment.Rehydrate(
		new RecurringPaymentId(LegacyValues.IdOrDerived(Id, PayeeMatch.SignatureOf(SeriesKeys ?? []), Name)),
		Name ?? "",
		LegacyValues.Category(CategoryId),
		new PayeeMatch(SeriesKeys ?? [], Tokens ?? []),
		new RecurrenceRule(Amount, TolerancePct, CadenceDays),
		Active,
		CreatedAt);
	#endregion
}
