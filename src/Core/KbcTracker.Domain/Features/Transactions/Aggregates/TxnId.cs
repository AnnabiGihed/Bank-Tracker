namespace KbcTracker.Domain.Features.Transactions.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies a transaction: the bank's entry reference or transaction id, or a hash of the content
///              when the bank sends neither. Stored in the user's decisions, so it never changes for a transaction.
/// </summary>
/// <param name="Value">The identifier value.</param>
public sealed record TxnId(string Value) : StronglyTypedStringId<TxnId>(Value);
