namespace KbcTracker.Application.Features.BankConnection.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What one download of an account's transactions gave: the transactions that could be read, and how
///              many could not, so the sync can tell the user instead of losing them silently.
/// </summary>
/// <param name="Transactions">The transactions that could be read.</param>
/// <param name="Skipped">How many transactions the bank sent that could not be read and were left out.</param>
public sealed record BankDownload(IReadOnlyList<Txn> Transactions, int Skipped);
