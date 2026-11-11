using Pivot.Framework.Application.Abstractions.Messaging.Commands;

namespace KbcTracker.Application.Features.BankConnection.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests a bank sync through the shared application workflow.
/// </summary>
/// <param name="Full">Whether to request the longest available bank history.</param>
public sealed record SyncBankCommand(bool Full = false) : ICommand<SyncSummary>;
