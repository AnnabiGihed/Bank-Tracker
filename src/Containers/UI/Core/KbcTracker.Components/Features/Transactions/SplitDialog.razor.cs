namespace KbcTracker.Components.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The dialog that splits a payment into categorised parts (ADR-0007): a row per part with a label, a
///              category and an amount, the amount left to assign, and Save (blocked until the parts add up exactly), Remove
///              split and Cancel. It validates through <see cref="SplitEditor"/> and closes with the decision to send, or
///              with nothing when cancelled; it sends nothing itself.
/// </summary>
public partial class SplitDialog : ComponentBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that closes the dialog.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets the editor that holds the parts and checks them.</summary>
	[EditorRequired]
	[Parameter]
	public required SplitEditor Editor { get; set; }
	#endregion

	#region Event Handlers
	/// <summary>Adds an empty part.</summary>
	private void AddPart() => Editor.AddPart();

	/// <summary>Closes the dialog with the split to save, when the parts are valid.</summary>
	private void Save()
	{
		if (Editor.IsValid)
		{
			Dialogs.Close(Editor.ToRequest());
		}
	}

	/// <summary>Closes the dialog with the request that removes the split.</summary>
	private void Unsplit() => Dialogs.Close(Editor.UnsplitRequest());

	/// <summary>Closes the dialog without a decision.</summary>
	private void Cancel() => Dialogs.Close(null);
	#endregion
}
