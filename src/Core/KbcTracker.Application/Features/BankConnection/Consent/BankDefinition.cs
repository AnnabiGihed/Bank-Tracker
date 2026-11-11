namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Identifies an available bank without exposing provider credentials.
/// </summary>
/// <param name="Name">The provider's bank name.</param>
/// <param name="Country">The bank's ISO country code.</param>
public sealed record BankDefinition(string Name, string Country);
