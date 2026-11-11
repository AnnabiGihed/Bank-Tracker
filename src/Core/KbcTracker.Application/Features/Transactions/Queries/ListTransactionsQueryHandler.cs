using KbcTracker.Domain.Features.Transactions.Repositories;
using Pivot.Framework.Application.Abstractions.Messaging.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Transactions.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Validates paging and projects history to the public read contract.
/// </summary>
/// <param name="transactions">The central transaction history.</param>
internal sealed class ListTransactionsQueryHandler(ITransactionRepository transactions)
	: IQueryHandler<ListTransactionsQuery, IReadOnlyList<TransactionSummary>>
{
	#region IQueryHandler Implementation
	/// <inheritdoc />
	public async Task<Result<IReadOnlyList<TransactionSummary>>> Handle(ListTransactionsQuery request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (request.Skip < 0 || request.Take is < 1 or > 500)
		{
			return Result.Failure<IReadOnlyList<TransactionSummary>>(new Error("Transactions.Page.Invalid", "Skip must not be negative and take must be between 1 and 500."));
		}

		var page = await transactions.ListAsync(request.Skip, request.Take, cancellationToken).ConfigureAwait(false);
		IReadOnlyList<TransactionSummary> result = [.. page.Select(t => new TransactionSummary(t.Id.Value, t.Date, t.Amount, t.Currency, t.Counterparty, t.Description))];
		return Result.Success(result);
	}
	#endregion
}
