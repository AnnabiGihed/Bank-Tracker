namespace KbcTracker.ViewModels.Features.Subscriptions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Subscriptions tab shows: the suggested and the accepted subscriptions, that is the recurring
///              payments paid by card only and repeating monthly or yearly (<see cref="SubscriptionRule"/>), with their
///              cost per month and per year and its totals. Accepting, dismissing, editing and stopping use the same
///              requests as fixed payments, and the Overview's "Fixed" figure still counts them.
/// </summary>
public sealed class SubscriptionsViewModel
{
	#region Constants
	/// <summary>The number of months in a year, to convert a monthly cost to a yearly one and back.</summary>
	private const int MonthsPerYear = 12;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="SubscriptionsViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>, for the status of each subscription.</param>
	/// <param name="formats">The formats of amounts, dates and months.</param>
	/// <param name="today">Today's date, which decides whether a charge is due or late.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public SubscriptionsViewModel(Analysis analysis, string month, Formats formats, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		Suggestions = [.. analysis.Suggestions.Where(s => s.IsSubscription).Select(s => FixedSuggestion.Of(analysis, s, formats))];
		var subscriptions = analysis.Fixed.Where(f => f.IsSubscription).ToList();
		var costs = subscriptions.Select(f => (Payment: f, PerMonth: PerMonthOf(f.Def), PerYear: PerMonthOf(f.Def) * MonthsPerYear)).ToList();
		PerMonth = costs.Sum(c => c.PerMonth);
		PerYear = costs.Sum(c => c.PerYear);
		PerMonthText = formats.Money(PerMonth);
		PerYearText = formats.Money(PerYear);
		SubscriptionCount = subscriptions.Count;
		Rows = [.. costs.OrderByDescending(c => c.PerMonth).ThenBy(c => c.Payment.Def.Name, formats.NameComparer)
			.Select(c => ToRow(c.Payment, c.PerMonth, c.PerYear, month, today, formats))];
		CategoryGroups = CategoryOptions.Grouped(analysis, formats);
	}
	#endregion

	#region Properties
	/// <summary>Gets the suggested subscriptions to check.</summary>
	public IReadOnlyList<FixedSuggestion> Suggestions { get; }

	/// <summary>Gets what the accepted subscriptions cost per month.</summary>
	public decimal PerMonth { get; }

	/// <summary>Gets <see cref="PerMonth"/>, formatted.</summary>
	public string PerMonthText { get; }

	/// <summary>Gets what the accepted subscriptions cost per year.</summary>
	public decimal PerYear { get; }

	/// <summary>Gets <see cref="PerYear"/>, formatted.</summary>
	public string PerYearText { get; }

	/// <summary>Gets the number of accepted subscriptions.</summary>
	public int SubscriptionCount { get; }

	/// <summary>Gets the accepted subscriptions, the dearest per month first.</summary>
	public IReadOnlyList<SubscriptionRow> Rows { get; }

	/// <summary>Gets the categories the edit form offers, grouped by type.</summary>
	public IReadOnlyList<CategoryGroup> CategoryGroups { get; }

	/// <summary>Gets the note that says subscriptions are still fixed payments in the totals.</summary>
	public static string FixedNote => "Subscriptions still count as fixed payments in the totals, such as the Fixed payments figure of the Overview.";

	/// <summary>Gets the message shown when there is no accepted subscription.</summary>
	public static string EmptyMessage => "No confirmed subscriptions yet. Accept the suggestions above: card payments that come back every month or year show up here.";

	/// <summary>Gets the question asked before a subscription stops being tracked.</summary>
	public static string StopTrackingPrompt => "Stop tracking this subscription?";
	#endregion

	#region Public Methods
	/// <summary>Builds the request that stops tracking a subscription.</summary>
	/// <param name="row">The subscription.</param>
	/// <returns>The <c>removeFixed</c> request.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="row"/> is <see langword="null"/>.</exception>
	public static DecisionRequest StopTracking(SubscriptionRow row)
	{
		ArgumentNullException.ThrowIfNull(row);
		return DecisionRequests.RemoveFixed(row.Id);
	}
	#endregion

	#region Private Helpers
	/// <summary>Converts a subscription's usual amount to what it costs per month: a yearly one counts a twelfth.</summary>
	private static decimal PerMonthOf(RecurringPayment payment) =>
		string.Equals(Analysis.CadenceName(payment.CadenceDays), "Yearly", StringComparison.Ordinal) ? payment.Amount / MonthsPerYear : payment.Amount;

	/// <summary>Builds the row of an accepted subscription.</summary>
	private static SubscriptionRow ToRow(FixedRow payment, decimal perMonth, decimal perYear, string month, DateOnly today, Formats formats) => new(
		payment.Def.Id.Value, payment.Def.Name, payment.Category.Id.Value, payment.Category.Name, payment.Category.Color, Analysis.CadenceName(payment.Def.CadenceDays),
		formats.Money(payment.Def.Amount), formats.Money(perMonth), formats.Money(perYear), formats.ShortDate(payment.LastPaid), NextExpected(payment, formats),
		FixedStatusPill.Of(payment, payment.StatusIn(month, today), formats),
		new FixedPaymentEdit(payment.Def.Id.Value, payment.Def.Name, payment.Category.Id.Value, payment.Def.Amount, payment.Def.CadenceDays));

	/// <summary>Projects the last charge forward by one rhythm. A charge that is overdue keeps its date, which the status pill calls late.</summary>
	private static string NextExpected(FixedRow payment, Formats formats) =>
		payment.LastPaid is { } last ? formats.ShortDate(last.AddDays(payment.Def.CadenceDays)) : "—";
	#endregion
}
