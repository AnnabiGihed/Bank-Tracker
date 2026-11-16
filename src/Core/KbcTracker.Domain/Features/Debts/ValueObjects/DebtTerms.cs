namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the owner says about a debt: the values <see cref="Debt.Create"/> and <see cref="Debt.Revise"/>
///              check and keep. Not checked itself.
/// </summary>
/// <param name="Kind">Whether the money came from a person or a bank.</param>
/// <param name="Lender">Who lent it; 1 to 100 characters.</param>
/// <param name="Iban">The lender's IBAN, or <see langword="null"/>; it identifies the repayments to a person.</param>
/// <param name="ReceivedPaymentId">The credit that paid the money out, or <see langword="null"/> when the loan is older
/// than the bank's history.</param>
/// <param name="Principal">The amount received, positive with at most two decimals.</param>
/// <param name="SeriesKey">The <c>LOAN:</c> series of a bank loan's instalments, or <see langword="null"/>.</param>
/// <param name="Plan">How it is repaid, or <see langword="null"/> while the owner has not said.</param>
/// <param name="BorrowedOn">The day the money was received; only payments dated on or after it can repay the debt.</param>
public sealed record DebtTerms(DebtKind Kind, string Lender, string? Iban, TxnId? ReceivedPaymentId, decimal Principal, string? SeriesKey, RepaymentPlan? Plan, DateOnly BorrowedOn);
