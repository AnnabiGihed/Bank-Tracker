namespace KbcTracker.Domain.Features.BankConnection.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies a bank connection: the session id Enable Banking returns when the user gives consent.
/// </summary>
/// <param name="Value">The identifier value.</param>
public sealed record BankSessionId(string Value) : StronglyTypedStringId<BankSessionId>(Value);
