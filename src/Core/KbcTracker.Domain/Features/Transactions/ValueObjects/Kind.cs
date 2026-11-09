namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What kind of movement a transaction is, decided from the ISO bank transaction code KBC sends
///              (<c>bank_transaction_code.code/sub_code</c>) and, for transfers, from the account holder's name.
/// </summary>
public enum Kind
{
	/// <summary>
	/// A card payment (<c>CCRD</c> debit).
	/// </summary>
	Card,

	/// <summary>
	/// A SEPA direct debit (<c>IDDT</c>); one payee per mandate.
	/// </summary>
	DirectDebit,

	/// <summary>
	/// A loan instalment (<c>CSLN</c>).
	/// </summary>
	Loan,

	/// <summary>
	/// An investment or savings plan (<c>SETT/SUBS</c>).
	/// </summary>
	SavingsPlan,

	/// <summary>
	/// An account fee (<c>OPCL</c> or <c>ACMT</c>).
	/// </summary>
	BankFee,

	/// <summary>
	/// A transfer to someone else.
	/// </summary>
	Transfer,

	/// <summary>
	/// A transfer to the account holder's own name.
	/// </summary>
	OwnTransferOut,

	/// <summary>
	/// Spare-change investing (<c>ICCN</c> with "spare change" in the description).
	/// </summary>
	SpareChange,

	/// <summary>
	/// A cash withdrawal (<c>CCRD/CWDL</c>).
	/// </summary>
	CashWithdrawal,

	/// <summary>
	/// Money received from someone else.
	/// </summary>
	Income,

	/// <summary>
	/// Money received from the account holder's own name.
	/// </summary>
	OwnTransferIn,

	/// <summary>
	/// A loan paid out to the account (<c>RCDT/CAJT</c>); not income.
	/// </summary>
	LoanReceived,

	/// <summary>
	/// A cash deposit (<c>CCRD/CDPT</c>).
	/// </summary>
	CashDeposit,

	/// <summary>
	/// Money back on a card (<c>CCRD</c> credit).
	/// </summary>
	Refund,

	/// <summary>
	/// Anything the codes do not identify.
	/// </summary>
	Other,
}
