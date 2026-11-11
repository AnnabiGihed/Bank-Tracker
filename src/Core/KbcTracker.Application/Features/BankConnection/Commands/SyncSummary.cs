namespace KbcTracker.Application.Features.BankConnection.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reports a completed bank sync without exposing provider responses.
/// </summary>
/// <param name="Added">The count of new transactions durably added.</param>
public sealed record SyncSummary(int Added);
