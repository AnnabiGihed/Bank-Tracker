namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment that counts as a repayment of a debt, with the request of "Not a repayment".
/// </summary>
/// <param name="Id">The payment's transaction id.</param>
/// <param name="DateText">The booking date, as <c>dd/MM/yy</c>.</param>
/// <param name="Name">The payee's name.</param>
/// <param name="AmountText">The amount paid, formatted without a sign.</param>
/// <param name="AddedByHand">Whether the owner added the payment, rather than the automatic matching finding it.</param>
/// <param name="NotARepaymentRequest">The <c>unmatchRepayment</c> request that stops the payment counting.</param>
public sealed record DebtPaymentRow(string Id, string DateText, string Name, string AmountText, bool AddedByHand, DecisionRequest NotARepaymentRequest);
