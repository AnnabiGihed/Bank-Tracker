namespace KbcTracker.ViewModels.Features.Subscriptions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One accepted subscription as the Subscriptions tab lists it: what it costs per charge, per month and per
///              year, when it was last charged, when it is expected next and where it stands in the selected month.
/// </summary>
/// <param name="Id">The recurring payment.</param>
/// <param name="Name">Its name.</param>
/// <param name="CategoryId">Its category id.</param>
/// <param name="CategoryName">Its category name.</param>
/// <param name="CategoryColor">Its category colour.</param>
/// <param name="Rhythm">Monthly or Yearly.</param>
/// <param name="AmountText">Its usual amount per charge, formatted.</param>
/// <param name="PerMonthText">What it costs per month, formatted; a yearly one counts a twelfth.</param>
/// <param name="PerYearText">What it costs per year, formatted; a monthly one counts twelve charges.</param>
/// <param name="LastChargeText">When it was last charged, as <c>dd/MM/yy</c>.</param>
/// <param name="NextExpectedText">When it is expected next, as <c>dd/MM/yy</c>, or a dash when it was never charged.</param>
/// <param name="Status">Its status in the selected month.</param>
/// <param name="Edit">The form to edit it, filled with its current values.</param>
public sealed record SubscriptionRow(string Id, string Name, string CategoryId, string CategoryName, string CategoryColor, string Rhythm, string AmountText,
	string PerMonthText, string PerYearText, string LastChargeText, string NextExpectedText, FixedStatusPill Status, FixedPaymentEdit Edit);
