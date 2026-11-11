using Pivot.Framework.Application.Abstractions.Messaging.Queries;

namespace KbcTracker.Application.Features.BankConnection.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests client-safe bank connection status.
/// </summary>
public sealed record GetConnectionStatusQuery : IQuery<ConnectionStatus>;
