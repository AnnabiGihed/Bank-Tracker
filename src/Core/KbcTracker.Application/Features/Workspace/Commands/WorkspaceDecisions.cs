using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Errors;
using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.FixedPayments.Aggregates;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Splits.Errors;
using KbcTracker.Domain.Features.Splits.ValueObjects;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Applies the existing tracker decision vocabulary to the central domain aggregates.
/// </summary>
/// <param name="data">The tracked workspace.</param>
/// <param name="persistence">Stages category changes in the same unit of work.</param>
/// <param name="clock">The acceptance time provider.</param>
internal sealed class WorkspaceDecisions(WorkspaceData data, IWorkspacePersistence persistence, TimeProvider clock)
{
	#region Public Methods
	/// <summary>Validates and stages one supported decision without committing it.</summary>
	/// <param name="request">The UI action.</param>
	/// <returns>The UI response, or a validation/not-found failure.</returns>
	public Result<DecisionResponse> Apply(DecisionRequest request) => request.Action switch
	{
		"accept" when Has(request.Key) => Accept(request.Key!),
		"dismiss" when Has(request.Key) => Dismiss(request.Key!),
		"mark" when Has(request.Key) => Mark(request.Key!),
		"updateFixed" when Has(request.Id) => UnknownCategory(request.CategoryId)
			?? FromResult(data.User.UpdateFixedPayment(new RecurringPaymentId(request.Id!), request.Name,
				CategoryOrNull(request.CategoryId), request.Amount, request.Cadence)),
		"removeFixed" when Has(request.Id) => FromResult(data.User.RemoveFixedPayment(new RecurringPaymentId(request.Id!))),
		"payeeCategory" when Has(request.Key) => UnknownCategory(request.CategoryId) ?? AssignPayee(request.Key!, request.CategoryId),
		"txnOnly" when Has(request.Key) && Has(request.Id) && Has(request.CategoryId) => UnknownCategory(request.CategoryId) ?? AssignTransaction(request),
		"split" when Has(request.Id) && request.Parts is not null => Split(request.Id!, request.Parts),
		"unsplit" when Has(request.Id) => FromResult(data.User.RemoveSplit(new TxnId(request.Id!))),
		"confirm" => Confirm(request),
		"rename" when Has(request.Key) => FromResult(data.User.RenamePayee(request.Key!, request.Name)),
		"saveCategory" => CategoryDecisions.Save(data, persistence, request),
		"deleteCategory" when Has(request.Id) => CategoryDecisions.Delete(data, persistence, request.Id!),
		_ => Result.Failure<DecisionResponse>(new Error("Workspace.Action.Invalid", "Unknown action, or a field it needs is missing.")),
	};
	#endregion

	#region Private Helpers
	/// <summary>Accepts a still-existing computed suggestion.</summary>
	private Result<DecisionResponse> Accept(string signature)
	{
		var suggestion = Analyse().Suggestions.FirstOrDefault(s => string.Equals(s.Signature, signature, StringComparison.Ordinal));
		return suggestion is null ? Missing("This suggestion no longer exists.") : AcceptDefinition(suggestion);
	}

	/// <summary>Remembers an explicitly dismissed suggestion.</summary>
	private Result<DecisionResponse> Dismiss(string signature)
	{
		data.User.DismissSuggestion(signature);
		return Result.Success(new DecisionResponse());
	}

	/// <summary>Marks a payee with actual debit history as fixed.</summary>
	private Result<DecisionResponse> Mark(string seriesKey)
	{
		var analysis = Analyse();
		return !analysis.Items.Any(i => i.T.Amount < 0 && string.Equals(i.SeriesKey, seriesKey, StringComparison.Ordinal))
			? Missing("This payee has no payments to mark.") : AcceptDefinition(analysis.GroupFor(seriesKey));
	}

	/// <summary>Creates the definition through the domain's matching rules.</summary>
	private Result<DecisionResponse> AcceptDefinition(KbcTracker.Domain.Features.FixedPayments.ValueObjects.Suggestion suggestion)
	{
		var accepted = Analysis.ToDefinition(suggestion, RecurringPaymentId.New(), clock.GetUtcNow());
		if (accepted.IsFailure)
		{
			return Result.Failure<DecisionResponse>(accepted.Error, accepted.ResultExceptionType);
		}

		data.User.AcceptFixedPayment(accepted.Value);
		return Result.Success(new DecisionResponse());
	}

	/// <summary>Returns the previous payee assignment so the UI can undo it for a transaction-only change.</summary>
	private Result<DecisionResponse> AssignPayee(string key, string? categoryId)
	{
		var previous = data.User.Payees.TryGetValue(key, out var assignment) ? PayeeAssignmentSnapshot.From(assignment) : null;
		data.User.AssignPayeeCategory(key, CategoryOrNull(categoryId));
		return Result.Success(new DecisionResponse(PreviousAssignment: previous));
	}

	/// <summary>Restores the payee assignment while categorising one transaction.</summary>
	private Result<DecisionResponse> AssignTransaction(DecisionRequest request)
	{
		data.User.RestorePayeeCategory(request.Key!, request.Restore?.ToDomain());
		data.User.AssignTransactionCategory(new TxnId(request.Id!), new CategoryId(request.CategoryId!));
		return Result.Success(new DecisionResponse());
	}

	/// <summary>Divides an existing spending payment (or refund) into spending parts that add up to its amount.</summary>
	private Result<DecisionResponse> Split(string txnId, IReadOnlyList<SplitPartRequest> parts)
	{
		var id = new TxnId(txnId);
		var payment = data.Transactions.FirstOrDefault(t => t.Id == id);
		if (payment is null)
		{
			return Missing("This payment does not exist.");
		}

		// Splitting income or savings into spending parts would silently move it out of its own totals (ADR-0007).
		if (Analyse().Items.First(i => i.T.Id == id).Category.Type != CategoryType.Expense)
		{
			return Result.Failure<DecisionResponse>(PaymentSplitErrors.NotSpendingPayment);
		}

		foreach (var part in parts)
		{
			var category = data.Categories.FirstOrDefault(c => string.Equals(c.Id.Value, part.CategoryId, StringComparison.Ordinal));
			if (category is null)
			{
				return Result.Failure<DecisionResponse>(CategoryErrors.Unknown);
			}

			if (category.Type != CategoryType.Expense)
			{
				return Result.Failure<DecisionResponse>(PaymentSplitErrors.NotExpenseCategory);
			}
		}

		var split = PaymentSplit.Create(payment.Amount, parts.Select(p => new SplitLine(p.Amount, new CategoryId(p.CategoryId), p.Label)));
		if (split.IsFailure)
		{
			return Result.Failure<DecisionResponse>(split.Error, split.ResultExceptionType);
		}

		data.User.SplitPayment(id, split.Value);
		return Result.Success(new DecisionResponse());
	}

	/// <summary>Confirms each requested payee using its current centrally computed category.</summary>
	private Result<DecisionResponse> Confirm(DecisionRequest request)
	{
		var analysis = Analyse();
		var keys = request.Keys ?? (Has(request.Key) ? [request.Key!] : []);
		foreach (var key in keys.Where(Has))
		{
			var item = analysis.Items.FirstOrDefault(i => string.Equals(i.SeriesKey, key, StringComparison.Ordinal));
			if (item is not null)
			{
				data.User.AssignPayeeCategory(key, item.Category.Id);
			}
		}

		return Result.Success(new DecisionResponse());
	}

	/// <summary>Computes decision candidates using the complete backend source records.</summary>
	private Analysis Analyse() => new(data.Transactions, data.Categories, data.User, DateOnly.FromDateTime(clock.GetLocalNow().Date));

	/// <summary>Refuses a chosen category that does not exist, so a decision never points to nothing.</summary>
	/// <returns>The failure, or <see langword="null"/> when no category was chosen or it exists.</returns>
	private Result<DecisionResponse>? UnknownCategory(string? categoryId) =>
		!Has(categoryId) || data.Categories.Any(c => string.Equals(c.Id.Value, categoryId, StringComparison.Ordinal)) ? null
			: Result.Failure<DecisionResponse>(CategoryErrors.Unknown);

	/// <summary>Normalises an optional category without inventing a new identifier.</summary>
	private static CategoryId? CategoryOrNull(string? value) => Has(value) ? new CategoryId(value!) : null;

	/// <summary>Identifies fields required by a decision before calling domain guards.</summary>
	private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);

	/// <summary>Preserves domain failure semantics for the API controller.</summary>
	private static Result<DecisionResponse> FromResult(Result result) => result.IsSuccess
		? Result.Success(new DecisionResponse()) : Result.Failure<DecisionResponse>(result.Error, result.ResultExceptionType);

	/// <summary>Reports a stale UI selection as missing.</summary>
	private static Result<DecisionResponse> Missing(string message) => Result.Failure<DecisionResponse>(
		new Error("Workspace.Selection.NotFound", message), ResultExceptionType.NotFound);
	#endregion
}
