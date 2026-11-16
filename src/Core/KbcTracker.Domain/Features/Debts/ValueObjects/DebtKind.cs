namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Who the money was borrowed from, which decides how its repayments are recognised (ADR-0010).
/// </summary>
public enum DebtKind
{
	/// <summary>
	/// Money borrowed from a person; repaid by transfers to the lender's IBAN or name.
	/// </summary>
	PersonalLoan,

	/// <summary>
	/// A loan from a bank; repaid by the instalments of a <c>LOAN:</c> series.
	/// </summary>
	BankLoan,
}
