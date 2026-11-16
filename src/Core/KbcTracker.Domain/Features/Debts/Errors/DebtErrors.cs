namespace KbcTracker.Domain.Features.Debts.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors tracking a debt can fail with, coded <c>Debt.Property.Reason</c>.
/// </summary>
public static class DebtErrors
{
	#region Static Instances
	/// <summary>
	/// The debt does not exist (any more).
	/// </summary>
	public static readonly Error NotFound = new("Debt.Id.NotFound", "This debt does not exist.");

	/// <summary>
	/// The lender's name is blank or longer than a hundred characters.
	/// </summary>
	public static readonly Error LenderInvalid = new("Debt.Lender.Invalid", "The lender needs a name of 1 to 100 characters.");

	/// <summary>
	/// The amount received is not positive or has more than two decimals.
	/// </summary>
	public static readonly Error PrincipalInvalid = new("Debt.Principal.Invalid", "The amount received must be positive with at most two decimals.");

	/// <summary>
	/// The IBAN is not made of at most 34 letters and digits.
	/// </summary>
	public static readonly Error IbanInvalid = new("Debt.Iban.Invalid", "The IBAN must have at most 34 letters and digits.");

	/// <summary>
	/// The series key of a bank loan does not start with <c>LOAN:</c>.
	/// </summary>
	public static readonly Error SeriesKeyInvalid = new("Debt.SeriesKey.Invalid", "A loan series key starts with LOAN:.");

	/// <summary>
	/// The day the money was borrowed is not a plausible date.
	/// </summary>
	public static readonly Error BorrowedOnInvalid = new("Debt.BorrowedOn.Invalid", "Say on which day the money was borrowed.");

	/// <summary>
	/// The received payment already belongs to another debt.
	/// </summary>
	public static readonly Error ReceivedPaymentTaken = new("Debt.ReceivedPayment.Taken", "Another debt already tracks this payment.");

	/// <summary>
	/// The received payment is no credit under Loan received.
	/// </summary>
	public static readonly Error ReceivedPaymentInvalid = new("Debt.ReceivedPayment.Invalid", "The received payment must be money in, in the Loan received category.");

	/// <summary>
	/// The payment does not exist.
	/// </summary>
	public static readonly Error PaymentNotFound = new("Debt.Payment.NotFound", "This payment does not exist.");

	/// <summary>
	/// The payment is money in, so it cannot repay a debt.
	/// </summary>
	public static readonly Error PaymentNotDebit = new("Debt.Payment.NotDebit", "Only money out can repay a debt.");

	/// <summary>
	/// The payment already counts for another debt.
	/// </summary>
	public static readonly Error PaymentTaken = new("Debt.Payment.Taken", "This payment already counts for another debt.");
	#endregion
}
