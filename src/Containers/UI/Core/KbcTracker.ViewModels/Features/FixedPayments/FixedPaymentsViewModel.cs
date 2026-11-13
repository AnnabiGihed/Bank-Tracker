namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Fixed payments tab shows: the suggestions to accept or dismiss, the month's expected, paid
///              and still-to-come totals, and the accepted fixed payments with their status. Computed as the old page did.
/// </summary>
public sealed class FixedPaymentsViewModel
{
	#region Constants
	/// <summary>How many of a suggestion's payments are shown as chips.</summary>
	private const int ChipCount = 6;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="FixedPaymentsViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="formats">The formats of amounts, dates and months.</param>
	/// <param name="today">Today's date, which decides whether a payment is due or late.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public FixedPaymentsViewModel(Analysis analysis, string month, Formats formats, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		MonthLabel = Formats.MonthName(month);
		Suggestions = [.. analysis.Suggestions.Select(g => ToSuggestion(analysis, g, formats))];
		var states = analysis.Fixed.Select(f => (Payment: f, State: f.StatusIn(month, today))).Where(x => x.State.Status != FixedStatus.NotStarted).ToList();
		var monthly = states.Where(x => x.State.Status != FixedStatus.NotThisMonth || x.State.Paid > 0).ToList();
		Expected = monthly.Sum(x => x.State.Paid > 0 ? x.State.Paid : x.Payment.Def.Amount);
		Paid = monthly.Sum(x => x.State.Paid);
		StillToCome = monthly.Where(x => x.State.Status is FixedStatus.Due or FixedStatus.Late).Sum(x => x.Payment.Def.Amount);
		FixedPaymentCount = analysis.Fixed.Count;
		ExpectedText = formats.Money(Expected);
		PaidText = formats.Money(Paid);
		StillToComeText = formats.Money(StillToCome);
		Rows = [.. states.OrderBy(x => x.Payment.UsualDay).Select(x => ToRow(x.Payment, x.State, formats))];
		CategoryGroups = CategoryOptions.Grouped(analysis, formats);
	}
	#endregion

	#region Properties
	/// <summary>Gets the selected month's English name and year.</summary>
	public string MonthLabel { get; }

	/// <summary>Gets the suggested fixed payments to check.</summary>
	public IReadOnlyList<FixedSuggestion> Suggestions { get; }

	/// <summary>Gets what the month's fixed payments are expected to cost, paid ones at what was paid.</summary>
	public decimal Expected { get; }

	/// <summary>Gets <see cref="Expected"/>, formatted.</summary>
	public string ExpectedText { get; }

	/// <summary>Gets what was paid of them in the month.</summary>
	public decimal Paid { get; }

	/// <summary>Gets <see cref="Paid"/>, formatted.</summary>
	public string PaidText { get; }

	/// <summary>Gets the usual amount of the ones still due or late.</summary>
	public decimal StillToCome { get; }

	/// <summary>Gets <see cref="StillToCome"/>, formatted.</summary>
	public string StillToComeText { get; }

	/// <summary>Gets the number of accepted fixed payments, whatever their status.</summary>
	public int FixedPaymentCount { get; }

	/// <summary>Gets the accepted fixed payments that started, ordered by the day they are usually paid.</summary>
	public IReadOnlyList<FixedPaymentRow> Rows { get; }

	/// <summary>Gets the categories the edit form offers, grouped by type.</summary>
	public IReadOnlyList<CategoryGroup> CategoryGroups { get; }

	/// <summary>Gets the message shown when there is no fixed payment to list.</summary>
	public static string EmptyMessage => "No confirmed fixed payments yet. Accept the suggestions above, or click ☆ next to a payment in Transactions.";

	/// <summary>Gets the question asked before a fixed payment stops being tracked.</summary>
	public static string StopTrackingPrompt => "Stop tracking this fixed payment?";
	#endregion

	#region Public Methods
	/// <summary>Builds the request that stops tracking a fixed payment.</summary>
	/// <param name="row">The fixed payment.</param>
	/// <returns>The <c>removeFixed</c> request.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
	public static DecisionRequest StopTracking(FixedPaymentRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		return DecisionRequests.RemoveFixed(row.Id);
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the card of a suggestion.</summary>
	private static FixedSuggestion ToSuggestion(Analysis analysis, Suggestion suggestion, Formats formats)
	{
		var category = analysis.CategoryById(suggestion.CategoryId);
		return new(suggestion.Signature, suggestion.Name, !suggestion.HighConfidence, formats.Money(suggestion.Amount), suggestion.Cadence.ToLowerInvariant(),
			suggestion.Reason, category.Name, category.Color,
			[.. suggestion.Items.TakeLast(ChipCount).Select(i => new SuggestionChip($"{formats.Day(i.T.Date)} · {formats.Money(-i.T.Amount)}", i.T.Description))]);
	}

	/// <summary>Builds the row of an accepted fixed payment.</summary>
	private static FixedPaymentRow ToRow(FixedRow payment, (FixedStatus Status, decimal Paid, DateOnly? Date) state, Formats formats) => new(
		payment.Def.Id.Value, payment.Def.Name, payment.Category.Id.Value, payment.Category.Name, payment.Category.Color, payment.UsualDay,
		Analysis.CadenceName(payment.Def.CadenceDays), payment.Payments.Count, formats.ShortDate(payment.LastPaid), payment.Def.Amount,
		formats.Money(payment.Def.Amount), FixedStatusPill.Of(payment, state, formats),
		$"{Analysis.CadenceName(payment.Def.CadenceDays)} · {formats.Number(payment.Payments.Count)} payment(s) · last {formats.ShortDate(payment.LastPaid)}",
		new FixedPaymentEdit(payment.Def.Id.Value, payment.Def.Name, payment.Category.Id.Value, payment.Def.Amount, payment.Def.CadenceDays));
	#endregion
}
