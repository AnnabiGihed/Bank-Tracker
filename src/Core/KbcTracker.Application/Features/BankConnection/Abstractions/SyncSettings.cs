namespace KbcTracker.Application.Features.BankConnection.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The settings of a sync that do not belong to a particular bank client.
/// </summary>
/// <param name="HistoryMonths">How many months back a full sync asks for.</param>
public sealed record SyncSettings(int HistoryMonths);
