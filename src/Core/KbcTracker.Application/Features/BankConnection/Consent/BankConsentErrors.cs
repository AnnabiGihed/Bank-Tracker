using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Defines safe stable consent failures without provider responses, credentials or private paths.
/// </summary>
public static class BankConsentErrors
{
	#region Static Instances
	/// <summary>The provider or persistence operation could not complete.</summary>
	public static readonly Error Unavailable = new("BankConsent.Unavailable", "The bank connection could not complete. Start again later.");
	/// <summary>The selected bank or trusted owner identity is missing.</summary>
	public static readonly Error SelectionInvalid = new("BankConsent.Selection.Invalid", "Select a bank and country.");
	/// <summary>The callback does not match a live unconsumed request for this owner.</summary>
	public static readonly Error StateInvalid = new("BankConsent.State.Invalid", "Unknown or expired authorisation request. Start again.");
	/// <summary>The bank did not issue a successful consent code.</summary>
	public static readonly Error Denied = new("BankConsent.Denied", "Bank authorisation did not complete. Start again.");
	/// <summary>A running bank operation excludes consent replacement.</summary>
	public static readonly Error Busy = new("BankConsent.Busy", "A bank operation is running. Start consent again when it finishes.");
	/// <summary>The backend lacks the provider's application id or private key; names the settings, never their values.</summary>
	public static readonly Error NotConfigured = new("BankConsent.NotConfigured",
		"Bank access is not set up on the backend. Set EnableBanking:ApplicationId and EnableBanking:PrivateKeyPath in its user secrets, then restart it.");
	#endregion
}
