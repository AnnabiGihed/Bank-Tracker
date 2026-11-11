namespace KbcTracker.Application.Features.Transactions.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Returns display-safe transaction data without raw bank records, consent credentials or dedicated IBAN fields.
/// </summary>
/// <param name="Id">The transaction's stable identifier.</param>
/// <param name="Date">The booking date.</param>
/// <param name="Amount">The signed amount.</param>
/// <param name="Currency">The ISO currency.</param>
/// <param name="Name">The counterparty label.</param>
/// <param name="Description">The remittance description.</param>
public sealed record TransactionSummary(string Id, DateOnly Date, decimal Amount, string Currency, string Name, string Description);
