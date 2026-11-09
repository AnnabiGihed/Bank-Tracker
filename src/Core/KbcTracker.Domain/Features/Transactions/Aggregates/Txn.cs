namespace KbcTracker.Domain.Features.Transactions.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One bank transaction as stored in the permanent history.
///
///              Its shape is persisted (JSON today, SQLite later), so properties are only ever added, never renamed.
///              The bank's original JSON is kept in <see cref="Raw"/> because the analysis reads fields from it that
///              have no property here.
/// </summary>
/// <param name="Id">The bank's entry reference or transaction id, or a hash of the content when the bank sends neither.</param>
/// <param name="AccountUid">The Enable Banking account the transaction belongs to.</param>
/// <param name="Date">The booking date (or value or transaction date when the bank sends no booking date).</param>
/// <param name="Amount">The amount: negative for money out, positive for money in.</param>
/// <param name="Currency">The ISO currency code, usually <c>EUR</c>.</param>
/// <param name="Counterparty">The other party's name, or the start of the description when the bank sends no name.</param>
/// <param name="Description">The remittance information joined with the note, additional information and reference.</param>
/// <param name="CounterpartyIban">The other party's IBAN without spaces, when known.</param>
/// <param name="BankCode">The domain of the ISO bank transaction code (for example <c>PMNT</c>), when sent.</param>
/// <param name="Raw">The original JSON from the bank.</param>
public sealed record Txn(
	string Id,
	string AccountUid,
	DateOnly Date,
	decimal Amount,
	string Currency,
	string Counterparty,
	string Description,
	string? CounterpartyIban = null,
	string? BankCode = null,
	string? Raw = null);
