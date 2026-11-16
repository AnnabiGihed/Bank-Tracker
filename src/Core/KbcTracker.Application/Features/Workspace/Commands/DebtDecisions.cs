using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Aggregates;
using KbcTracker.Domain.Features.Debts.Aggregates;
using KbcTracker.Domain.Features.Debts.Errors;
using KbcTracker.Domain.Features.Debts.ValueObjects;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Validates and stages the debt decisions (ADR-0010): adding, changing and removing a debt, and correcting
///              which payments repay it. The checks that need the payments (the received credit and the matched
///              payment exist and are the right kind) live here; the rules about the debt itself are the domain's.
/// </summary>
internal static class DebtDecisions
{
	#region Public Methods
	/// <summary>Adds a debt with a new identifier.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="analyse">Builds the analysis, which tells the category of the received credit.</param>
	/// <param name="request">The request; its <see cref="DecisionRequest.Debt"/> must be set.</param>
	/// <returns>The new debt's identifier, or the failure that rejects the request.</returns>
	public static Result<DecisionResponse> Add(WorkspaceData data, Func<Analysis> analyse, DecisionRequest request)
	{
		var terms = TermsOf(data, analyse, request.Debt!, null);
		if (terms.IsFailure)
		{
			return Result.Failure<DecisionResponse>(terms.Error, terms.ResultExceptionType);
		}

		var id = DebtId.New();
		var added = data.User.AddDebt(id, terms.Value);
		return added.IsSuccess ? Result.Success(new DecisionResponse(id.Value)) : Result.Failure<DecisionResponse>(added.Error, added.ResultExceptionType);
	}

	/// <summary>Replaces what the owner said about a debt.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="analyse">Builds the analysis, which tells the category of the received credit.</param>
	/// <param name="request">The request; its <see cref="DecisionRequest.Id"/> and <see cref="DecisionRequest.Debt"/> must be set.</param>
	/// <returns>The debt's identifier, or the failure that rejects the request.</returns>
	public static Result<DecisionResponse> Update(WorkspaceData data, Func<Analysis> analyse, DecisionRequest request)
	{
		var debtId = new DebtId(request.Id!);
		var terms = TermsOf(data, analyse, request.Debt!, data.User.Debts.FirstOrDefault(d => d.Id == debtId)?.ReceivedPaymentId);
		if (terms.IsFailure)
		{
			return Result.Failure<DecisionResponse>(terms.Error, terms.ResultExceptionType);
		}

		var updated = data.User.UpdateDebt(debtId, terms.Value);
		return updated.IsSuccess ? Result.Success(new DecisionResponse(request.Id)) : Result.Failure<DecisionResponse>(updated.Error, updated.ResultExceptionType);
	}

	/// <summary>Stops tracking a debt.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="debtId">The debt's identifier.</param>
	/// <returns>Success, or the failure when the debt does not exist.</returns>
	public static Result<DecisionResponse> Remove(WorkspaceData data, string debtId) => FromResult(data.User.RemoveDebt(new DebtId(debtId)));

	/// <summary>Says that a payment repays a debt.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="debtId">The debt's identifier.</param>
	/// <param name="paymentId">The payment's identifier: it must exist and be money out.</param>
	/// <returns>Success, or the failure that rejects the request.</returns>
	public static Result<DecisionResponse> Match(WorkspaceData data, string debtId, string paymentId)
	{
		var checkedPayment = CheckPayment(data, paymentId);
		return checkedPayment ?? FromResult(data.User.IncludeRepayment(new DebtId(debtId), new TxnId(paymentId)));
	}

	/// <summary>Says that a payment does not repay a debt.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="debtId">The debt's identifier.</param>
	/// <param name="paymentId">The payment's identifier: it must exist and be money out.</param>
	/// <returns>Success, or the failure that rejects the request.</returns>
	public static Result<DecisionResponse> Unmatch(WorkspaceData data, string debtId, string paymentId)
	{
		var checkedPayment = CheckPayment(data, paymentId);
		return checkedPayment ?? FromResult(data.User.ExcludeRepayment(new DebtId(debtId), new TxnId(paymentId)));
	}

	/// <summary>Forgets what the owner said about a payment, so the automatic matching decides again.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="debtId">The debt's identifier.</param>
	/// <param name="paymentId">The payment's identifier.</param>
	/// <returns>Success, or the failure when the debt does not exist.</returns>
	public static Result<DecisionResponse> Reset(WorkspaceData data, string debtId, string paymentId) =>
		FromResult(data.User.ResetRepayment(new DebtId(debtId), new TxnId(paymentId)));
	#endregion

	#region Private Helpers
	/// <summary>Turns the request into checked terms: the plan, the received credit, the amount received and the day it was borrowed.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="analyse">Builds the analysis, which tells the category of the received credit.</param>
	/// <param name="request">The debt the owner described.</param>
	/// <param name="current">The received payment the debt already has: it is not checked again when it stays the same, because
	/// the owner may have recategorised it since.</param>
	/// <returns>The terms, or the failure that rejects the request.</returns>
	private static Result<DebtTerms> TermsOf(WorkspaceData data, Func<Analysis> analyse, DebtRequest request, TxnId? current)
	{
		RepaymentPlan? plan = null;
		if (request.Plan is not null)
		{
			var built = PlanOf(request.Plan);
			if (built.IsFailure)
			{
				return Result.Failure<DebtTerms>(built.Error, built.ResultExceptionType);
			}

			plan = built.Value;
		}

		Txn? received = null;
		if (!string.IsNullOrWhiteSpace(request.ReceivedPaymentId))
		{
			var found = ReceivedOf(data, analyse, request.ReceivedPaymentId, current);
			if (found.IsFailure)
			{
				return Result.Failure<DebtTerms>(found.Error, found.ResultExceptionType);
			}

			received = found.Value;
		}

		var borrowedOn = request.BorrowedOn ?? received?.Date;
		var principal = request.Principal ?? received?.Amount ?? request.Plan?.Amount ?? 0m;
		return borrowedOn is null
			? Result.Failure<DebtTerms>(DebtErrors.BorrowedOnInvalid)
			: new DebtTerms(request.Kind, request.Lender ?? "", request.Iban, received?.Id, principal, request.SeriesKey, plan, borrowedOn.Value);
	}

	/// <summary>Finds the received payment of a debt and checks that it is a loan credit, unless the debt already has it.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="analyse">Builds the analysis, which tells the category of the received credit.</param>
	/// <param name="paymentId">The identifier of the received payment.</param>
	/// <param name="current">The received payment the debt already has, which is not checked again.</param>
	/// <returns>The payment, or the failure when it is missing or not a loan credit.</returns>
	private static Result<Txn> ReceivedOf(WorkspaceData data, Func<Analysis> analyse, string paymentId, TxnId? current)
	{
		var received = data.Transactions.FirstOrDefault(t => string.Equals(t.Id.Value, paymentId, StringComparison.Ordinal));
		if (received is null)
		{
			return Result.Failure<Txn>(DebtErrors.PaymentNotFound, ResultExceptionType.NotFound);
		}

		return received.Id != current && !IsLoanCredit(analyse(), received.Id)
			? Result.Failure<Txn>(DebtErrors.ReceivedPaymentInvalid)
			: Result.Success(received);
	}

	/// <summary>Builds the plan the owner asked for: instalments with a frequency, otherwise one payment.</summary>
	/// <param name="request">The plan the owner described.</param>
	/// <returns>The plan, or the failure that rejects it.</returns>
	private static Result<RepaymentPlan> PlanOf(RepaymentPlanRequest request) =>
		request.Frequency is { } frequency
			? RepaymentPlan.Instalments(request.Amount, request.Start, frequency, request.DayOfMonth, request.InstalmentAmount, request.Count)
			: RepaymentPlan.OnePayment(request.Amount, request.Start);

	/// <summary>Determines whether a payment is money in, not split, under Loan received, Borrowed from people included.</summary>
	/// <param name="analysis">The analysis that categorises the payments.</param>
	/// <param name="id">The payment's identifier.</param>
	/// <returns><see langword="true"/> when the payment is a loan credit.</returns>
	private static bool IsLoanCredit(Analysis analysis, TxnId id)
	{
		var item = analysis.Items.FirstOrDefault(i => i.T.Id == id);
		return item is not null && item.T.Amount > 0 && !item.IsSplit
			&& (item.Category.Id == CategoryId.LoanReceived || analysis.Tree.Ancestors(item.Category.Id.Value).Any(a => a.Id == CategoryId.LoanReceived));
	}

	/// <summary>Checks that a payment exists and is money out.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="paymentId">The payment's identifier.</param>
	/// <returns>The failure, or <see langword="null"/> when the payment is fine.</returns>
	private static Result<DecisionResponse>? CheckPayment(WorkspaceData data, string paymentId)
	{
		var payment = data.Transactions.FirstOrDefault(t => string.Equals(t.Id.Value, paymentId, StringComparison.Ordinal));
		if (payment is null)
		{
			return Result.Failure<DecisionResponse>(DebtErrors.PaymentNotFound, ResultExceptionType.NotFound);
		}

		return payment.Amount < 0 ? null : Result.Failure<DecisionResponse>(DebtErrors.PaymentNotDebit);
	}

	/// <summary>Preserves domain failure semantics for the API controller.</summary>
	private static Result<DecisionResponse> FromResult(Result result) => result.IsSuccess
		? Result.Success(new DecisionResponse()) : Result.Failure<DecisionResponse>(result.Error, result.ResultExceptionType);
	#endregion
}
