namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One accepted fixed payment as the Fixed payments tab lists it, with its status in the month.
/// </summary>
/// <param name="Id">The fixed payment.</param>
/// <param name="Name">Its name.</param>
/// <param name="CategoryId">Its category id.</param>
/// <param name="CategoryName">Its category name.</param>
/// <param name="CategoryColor">Its category colour.</param>
/// <param name="UsualDay">The day of the month it is usually paid.</param>
/// <param name="Rhythm">Monthly, Quarterly or Yearly.</param>
/// <param name="PaymentCount">How many payments were matched to it.</param>
/// <param name="LastPaid">When it was last paid, as <c>dd/MM/yy</c>.</param>
/// <param name="Amount">Its usual amount.</param>
/// <param name="AmountText">Its usual amount, formatted.</param>
/// <param name="Status">Its status in the month.</param>
/// <param name="DetailText">The line under its name: rhythm, number of payments and when it was last paid.</param>
/// <param name="Edit">The form to edit it, filled with its current values.</param>
public sealed record FixedPaymentRow(string Id, string Name, string CategoryId, string CategoryName, string CategoryColor, int UsualDay, string Rhythm,
	int PaymentCount, string LastPaid, decimal Amount, string AmountText, FixedStatusPill Status, string DetailText, FixedPaymentEdit Edit);
