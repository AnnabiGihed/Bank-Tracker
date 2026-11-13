namespace KbcTracker.Components.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The dialog that creates or changes a category: name, parent, type (for a top-level category), colour and
///              keywords, with Delete where the category may be deleted (after a confirmation that says where its
///              payments go). It validates through <see cref="CategoryEdit"/> and closes with the decision to send, or with
///              nothing when cancelled; it sends nothing itself.
/// </summary>
public partial class CategoryDialog : ComponentBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens the confirmation and closes the dialog.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets what is being created or changed.</summary>
	[EditorRequired]
	[Parameter]
	public required CategoryEdit Edit { get; set; }
	#endregion

	#region Properties
	/// <summary>Gets the category types the owner can choose from.</summary>
	private static IReadOnlyList<TypeChoice> TypeChoices { get; } =
	[
		new(CategoryType.Expense, CategoryOptions.TypeLabel(CategoryType.Expense)),
		new(CategoryType.Income, CategoryOptions.TypeLabel(CategoryType.Income)),
		new(CategoryType.Savings, "Savings (not spending)"),
		new(CategoryType.Transfer, "Own transfer (ignored)"),
	];

	/// <summary>Gets the accessible name of the type drop-down.</summary>
	private static IReadOnlyDictionary<string, object> TypeInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-label"] = "Counts as" };
	#endregion

	#region Event Handlers
	/// <summary>Takes over the colour the owner picked.</summary>
	/// <param name="args">The change event of the colour input.</param>
	private void OnColorChanged(ChangeEventArgs args) => Edit.Color = args.Value?.ToString() ?? Edit.Color;

	/// <summary>Closes the dialog with the category to save, when it is valid.</summary>
	private void Save()
	{
		if (Edit.IsValid)
		{
			Dialogs.Close(Edit.ToRequest());
		}
	}

	/// <summary>Asks to delete the category and, when the owner agrees, closes the dialog with the request.</summary>
	/// <returns>A task that completes when the owner answered.</returns>
	private async Task DeleteAsync()
	{
		var confirmed = await Dialogs.Confirm(Edit.DeletePrompt(), "Delete category", new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Keep" });
		if (confirmed == true)
		{
			Dialogs.Close(Edit.DeleteRequest());
		}
	}

	/// <summary>Closes the dialog without a decision.</summary>
	private void Cancel() => Dialogs.Close(null);
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : One entry of the type drop-down.
	/// </summary>
	/// <param name="Value">The category type.</param>
	/// <param name="Label">The text shown for it.</param>
	private sealed record TypeChoice(CategoryType Value, string Label);
	#endregion
}
