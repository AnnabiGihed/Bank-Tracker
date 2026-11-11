namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries provider session credentials only between backend components, never to a UI.
/// </summary>
/// <param name="SessionId">The provider's session credential.</param>
/// <param name="AccountUids">The accounts covered by the consent.</param>
public sealed record BankConsentSession(string SessionId, IReadOnlyList<string> AccountUids);
