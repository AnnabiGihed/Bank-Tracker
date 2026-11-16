namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How the owner repays a debt, the first choice of the repayment plan form.
/// </summary>
public enum RepaymentShape
{
	/// <summary>Everything in one payment on a due date.</summary>
	OnePayment,

	/// <summary>Several payments from a first date, as often as the frequency says.</summary>
	Instalments,
}
