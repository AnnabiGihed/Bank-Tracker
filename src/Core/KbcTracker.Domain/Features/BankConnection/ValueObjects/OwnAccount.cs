namespace KbcTracker.Domain.Features.BankConnection.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One of the owner's accounts the bank connection covers: the Enable Banking id, the IBAN, the name and the
///              ISO cash account type. The analysis recognises a payment to or from one of these IBANs as an own transfer,
///              and a transfer to a savings account as saved money (ADR-0010).
/// </summary>
/// <param name="Uid">The Enable Banking account id.</param>
/// <param name="Iban">The IBAN without spaces, or <see langword="null"/> when the bank did not send it.</param>
/// <param name="Name">The account's name as the bank gives it, or <see langword="null"/>.</param>
/// <param name="AccountType">The ISO 20022 cash account type (<c>CACC</c> current, <c>SVGS</c> savings), or <see langword="null"/>.</param>
public sealed record OwnAccount(string Uid, string? Iban, string? Name, string? AccountType)
{
	#region Constants
	/// <summary>
	/// The ISO cash account type of a current account.
	/// </summary>
	public const string CurrentType = "CACC";

	/// <summary>
	/// The ISO cash account type of a savings account.
	/// </summary>
	public const string SavingsType = "SVGS";
	#endregion

	#region Public Methods
	/// <summary>
	/// Brings an IBAN to the form used to compare accounts: no spaces, upper case.
	/// </summary>
	/// <param name="iban">The IBAN as written by the bank, or <see langword="null"/>.</param>
	/// <returns>The compact IBAN, or <see langword="null"/> when it is blank.</returns>
	public static string? NormaliseIban(string? iban) =>
		string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
	#endregion
}
