namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries provider session credentials only between backend components, never to a UI.
/// </summary>
/// <param name="SessionId">The provider's session credential.</param>
/// <param name="AccountUids">The accounts covered by the consent.</param>
/// <param name="Accounts">The IBAN, name and type of those accounts as far as the provider sent them, or <see langword="null"/>.</param>
public sealed record BankConsentSession(string SessionId, IReadOnlyList<string> AccountUids, IReadOnlyList<OwnAccount>? Accounts = null);
