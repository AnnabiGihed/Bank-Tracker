namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Returns only the bank's browser authorisation destination to the UI.
/// </summary>
/// <param name="AuthorizationUrl">The HTTPS URL at which the owner grants consent.</param>
public sealed record ConsentStarted(Uri AuthorizationUrl);
