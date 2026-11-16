using System.Globalization;

namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Words a repayment plan in one line, such as <c>12 × 250,00 €, last 180,00 €, 5 Jan – 5 Dec 2027</c>, for the
///              live preview of the plan form and the plan line of a debt card. Dates use English month names.
/// </summary>
public static class RepaymentPlanText
{
	#region Public Methods
	/// <summary>Words a plan.</summary>
	/// <param name="plan">The plan.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>One payment reads <c>250,00 € on 5 Jan 2027</c>; instalments read their count, amount, the last amount when it
	/// differs, and the first and last date.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="plan"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static string Describe(RepaymentPlan plan, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(formats);
		var schedule = plan.Schedule();
		var first = schedule[0].Due;
		var last = schedule[^1].Due;
		if (plan.IsOnePayment)
		{
			return $"{formats.Money(plan.Amount)} on {Date(first, withYear: true)}";
		}

		var amounts = plan.LastInstalmentAmount == plan.InstalmentAmount
			? $"{plan.Count} × {formats.Money(plan.InstalmentAmount)}"
			: $"{plan.Count} × {formats.Money(plan.InstalmentAmount)}, last {formats.Money(plan.LastInstalmentAmount)}";
		return $"{amounts}, {Date(first, withYear: first.Year != last.Year)} – {Date(last, withYear: true)}";
	}

	/// <summary>Names how often instalments fall due.</summary>
	/// <param name="frequency">The frequency.</param>
	/// <returns>The word the form shows.</returns>
	public static string Frequency(RepaymentFrequency frequency) => frequency switch
	{
		RepaymentFrequency.Weekly => "Weekly",
		RepaymentFrequency.Quarterly => "Quarterly",
		RepaymentFrequency.Yearly => "Yearly",
		_ => "Monthly",
	};
	#endregion

	#region Private Helpers
	/// <summary>Writes a date as <c>5 Jan</c>, or <c>5 Jan 2027</c> with the year.</summary>
	private static string Date(DateOnly date, bool withYear) => date.ToString(withYear ? "d MMM yyyy" : "d MMM", CultureInfo.InvariantCulture);
	#endregion
}
