using KbcTracker.Application.Features.BankConnection.Queries;
using Pivot.Framework.Application.Abstractions.Messaging.Commands;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Completes a bank redirect without exposing its code or provider errors to a client response.
/// </summary>
/// <param name="Code">The provider's one-use code.</param>
/// <param name="State">The request state echoed by the provider.</param>
/// <param name="Error">The provider's failure marker, if present.</param>
/// <param name="Owner">The trusted caller identity.</param>
public sealed record CompleteBankConsentCommand(string? Code, string? State, string? Error, string Owner) : ICommand<ConnectionStatus>;
