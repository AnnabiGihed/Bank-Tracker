namespace KbcTracker.Domain.Features.Transactions.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors a transaction can fail to be created with, coded <c>Txn.Property.Reason</c>.
/// </summary>
public static class TxnErrors
{
	#region Static Instances
	/// <summary>
	/// The account the transaction belongs to is missing.
	/// </summary>
	public static readonly Error AccountRequired = new("Txn.AccountUid.Required", "The transaction's account is required.");

	/// <summary>
	/// The currency is not a three-letter ISO 4217 code.
	/// </summary>
	public static readonly Error CurrencyInvalid = new("Txn.Currency.Invalid", "The currency must be a three-letter code such as EUR.");

	/// <summary>
	/// The other party's name is missing.
	/// </summary>
	public static readonly Error CounterpartyRequired = new("Txn.Counterparty.Required", "The transaction's other party is required.");
	#endregion
}
