using Pivot.Framework.Application.Abstractions.Messaging.Queries;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests the bank catalogue through the central backend.
/// </summary>
public sealed record ListBanksQuery : IQuery<IReadOnlyList<BankDefinition>>;
