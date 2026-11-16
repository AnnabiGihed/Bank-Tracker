using KbcTracker.Domain.Features.Debts.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the owner says about a debt, as sent with the <c>addDebt</c> and <c>updateDebt</c> actions (ADR-0010).
/// </summary>
/// <param name="Kind">Whether the money came from a person or a bank.</param>
/// <param name="Lender">Who lent it; 1 to 100 characters.</param>
/// <param name="Iban">The lender's IBAN; it identifies the repayments to a person.</param>
/// <param name="ReceivedPaymentId">The credit that paid the money out: it must exist, be money in under Loan received
/// (Borrowed from people included) and belong to no other debt. Omitted for a loan older than the bank's history.</param>
/// <param name="Principal">The amount received. Omitted, it is the received payment's amount, else the plan's amount.</param>
/// <param name="SeriesKey">The <c>LOAN:</c> series of a bank loan's instalments.</param>
/// <param name="Plan">How it is repaid; omitted while the owner has not said, which marks the debt "No plan yet".</param>
/// <param name="BorrowedOn">The day the money was received; only payments on or after it can repay the debt. Omitted, it is the
/// received payment's date; it is required when there is no received payment.</param>
public sealed record DebtRequest(DebtKind Kind, string Lender, string? Iban = null, string? ReceivedPaymentId = null, decimal? Principal = null,
	string? SeriesKey = null, RepaymentPlanRequest? Plan = null, DateOnly? BorrowedOn = null);
