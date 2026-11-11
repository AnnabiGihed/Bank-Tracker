namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Selects the bank whose read-only consent the owner wants to grant.
/// </summary>
/// <param name="Name">The bank name from the bank catalogue.</param>
/// <param name="Country">The bank's country.</param>
public sealed record BeginConsentRequest(string Name, string Country);
