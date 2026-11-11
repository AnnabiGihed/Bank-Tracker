namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Supplies the configured bank consent duration to persistence without provider dependencies.
/// </summary>
/// <param name="ConsentDays">The requested lifetime, between 1 and 180 days.</param>
public sealed record BankConsentSettings(int ConsentDays);
