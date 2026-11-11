using Pivot.Framework.Application.Abstractions.Messaging.Commands;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Begins consent bound to the trusted caller identity, rather than a client-supplied owner field.
/// </summary>
/// <param name="Selection">The selected bank.</param>
/// <param name="Owner">The identity established by the API boundary.</param>
public sealed record BeginBankConsentCommand(BeginConsentRequest Selection, string Owner) : ICommand<ConsentStarted>;
