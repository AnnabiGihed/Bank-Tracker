using Pivot.Framework.Application.Abstractions.Messaging.Queries;

namespace KbcTracker.Application.Features.Transactions.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests a bounded stable page of transaction history.
/// </summary>
/// <param name="Skip">The row offset, not negative.</param>
/// <param name="Take">The page size, between 1 and 500.</param>
public sealed record ListTransactionsQuery(int Skip = 0, int Take = 100) : IQuery<IReadOnlyList<TransactionSummary>>;
