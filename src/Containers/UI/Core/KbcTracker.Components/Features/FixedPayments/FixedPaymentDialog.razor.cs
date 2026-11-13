namespace KbcTracker.Components.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The dialog that edits a fixed payment: its name, category, usual amount and rhythm (monthly, quarterly,
///              yearly). It validates through <see cref="FixedPaymentEdit"/> and closes with the decision to send, or with
///              nothing when cancelled; it sends nothing itself.
/// </summary>
public partial class FixedPaymentDialog : ComponentBase
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
	public required FixedPaymentEdit Edit { get; set; }

	/// <summary>Gets or sets the categories to choose from.</summary>
	[Parameter]
	public IReadOnlyList<CategoryOption> CategoryOptions { get; set; } = [];
	#endregion

	#region Properties
	/// <summary>Gets the accessible name of the amount input: the visible label next to it.</summary>
	private static IReadOnlyDictionary<string, object> AmountInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-labelledby"] = "fixed-amount-label" };

	/// <summary>Gets the accessible name of the rhythm drop-down: the visible label next to it.</summary>
	private static IReadOnlyDictionary<string, object> RhythmInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-labelledby"] = "fixed-rhythm-label" };
	#endregion

	#region Event Handlers
	/// <summary>Closes the dialog with the update to save, when the entries are valid.</summary>
	private void Save()
	{
		if (Edit.IsValid)
		{
			Dialogs.Close(Edit.ToRequest());
		}
	}

	/// <summary>Closes the dialog without a decision.</summary>
	private void Cancel() => Dialogs.Close(null);
	#endregion
}
