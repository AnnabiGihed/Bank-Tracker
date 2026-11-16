namespace KbcTracker.Components.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The dialog that asks how a debt is repaid (ADR-0010): kind, lender and IBAN, the day it was borrowed, the amount
///              to repay, then one payment or instalments with their frequency and size, a live preview of the plan in words
///              and the problems that stop saving. It works on a <see cref="RepaymentPlanEdit"/> and closes with the decision
///              to send (Save, or Skip for now for a new debt), or with nothing when cancelled; it sends nothing itself.
/// </summary>
public partial class RepaymentPlanDialog : ComponentBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that closes the dialog.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets what is being edited.</summary>
	[EditorRequired]
	[Parameter]
	public required RepaymentPlanEdit Edit { get; set; }
	#endregion

	#region Properties
	/// <summary>Gets the frequencies the form offers, with their words.</summary>
	private static IReadOnlyList<(RepaymentFrequency Frequency, string Label)> Frequencies { get; } =
		[.. Enum.GetValues<RepaymentFrequency>().OrderBy(f => f == RepaymentFrequency.Monthly ? 0 : 1).ThenBy(f => f).Select(f => (f, RepaymentPlanText.Frequency(f)))];

	/// <summary>Gets the accessible name of the day-borrowed input.</summary>
	private static IReadOnlyDictionary<string, object> BorrowedInputAttributes { get; } = Labelled("plan-borrowed-label");

	/// <summary>Gets the accessible name of the amount to repay.</summary>
	private static IReadOnlyDictionary<string, object> AmountInputAttributes { get; } = Labelled("plan-amount-label");

	/// <summary>Gets the accessible name of the series picker.</summary>
	private static IReadOnlyDictionary<string, object> SeriesInputAttributes { get; } = Labelled("plan-series-label");

	/// <summary>Gets the accessible name of the date of the payment or of the first instalment.</summary>
	private static IReadOnlyDictionary<string, object> DateInputAttributes { get; } = Labelled("plan-date-label");

	/// <summary>Gets the accessible name of the frequency drop-down.</summary>
	private static IReadOnlyDictionary<string, object> FrequencyInputAttributes { get; } = Labelled("plan-frequency-label");

	/// <summary>Gets the accessible name of the amount of each instalment.</summary>
	private static IReadOnlyDictionary<string, object> EachInputAttributes { get; } = Labelled("plan-each-label");

	/// <summary>Gets the accessible name of the number of instalments.</summary>
	private static IReadOnlyDictionary<string, object> CountInputAttributes { get; } = Labelled("plan-count-label");
	#endregion

	#region Event Handlers
	/// <summary>Closes the dialog with the debt and its plan, when the entries are valid.</summary>
	private void Save()
	{
		if (Edit.IsValid)
		{
			Dialogs.Close(Edit.ToRequest());
		}
	}

	/// <summary>Closes the dialog with the debt but no plan, which marks it "No plan yet".</summary>
	private void Skip()
	{
		if (Edit.CanSkip)
		{
			Dialogs.Close(Edit.ToSkipRequest());
		}
	}

	/// <summary>Closes the dialog without a decision.</summary>
	private void Cancel() => Dialogs.Close(null);
	#endregion

	#region Private Helpers
	/// <summary>Builds the attributes that name an input by a visible label.</summary>
	private static Dictionary<string, object> Labelled(string labelId) => new(StringComparer.Ordinal) { ["aria-labelledby"] = labelId };
	#endregion
}
