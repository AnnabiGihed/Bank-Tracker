namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One debt as the Debts tab draws it: lender, kind, progress, what remains, what is due next, the status pill, the
///              plan in words, the schedule, the repayments found and the payments the owner can still add.
/// </summary>
/// <param name="Id">The debt's identifier.</param>
/// <param name="Lender">Who lent the money.</param>
/// <param name="KindText">Personal loan or Bank loan.</param>
/// <param name="Pill">The status pill.</param>
/// <param name="ProgressPercent">How much is repaid, from 0 to 100.</param>
/// <param name="ProgressText">What was repaid of what is to repay, such as <c>250,00 € of 1.000,00 €</c>.</param>
/// <param name="RemainingText">What is left to repay, formatted.</param>
/// <param name="NextText">The next payment in words, such as <c>250,00 € on 05/12/26</c>, or <see langword="null"/> when none is due.</param>
/// <param name="PlanText">The plan in words, or <see langword="null"/> while there is no plan.</param>
/// <param name="IsActive">Whether the debt is still being repaid.</param>
/// <param name="Schedule">The plan's payments with their state.</param>
/// <param name="Payments">The repayments found, oldest first.</param>
/// <param name="Addable">The debits the owner can add as a repayment, newest first.</param>
/// <param name="RemoveRequest">The <c>removeDebt</c> request.</param>
public sealed record DebtCard(string Id, string Lender, string KindText, DebtPill Pill, int ProgressPercent, string ProgressText, string RemainingText,
	string? NextText, string? PlanText, bool IsActive, IReadOnlyList<DebtScheduleLine> Schedule, IReadOnlyList<DebtPaymentRow> Payments,
	IReadOnlyList<PaymentOption> Addable, DecisionRequest RemoveRequest)
{
	#region Public Methods
	/// <summary>Builds the request that says a payment repays this debt.</summary>
	/// <param name="paymentId">The payment, one of <see cref="Addable"/>.</param>
	/// <returns>The <c>matchRepayment</c> request.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="paymentId"/> is <see langword="null"/>.</exception>
	public DecisionRequest AddPaymentRequest(string paymentId)
	{
		ArgumentNullException.ThrowIfNull(paymentId);
		return DecisionRequests.MatchRepayment(Id, paymentId);
	}
	#endregion
}
