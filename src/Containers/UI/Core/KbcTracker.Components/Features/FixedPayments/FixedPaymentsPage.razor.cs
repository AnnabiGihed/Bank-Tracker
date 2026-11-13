namespace KbcTracker.Components.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Fixed payments tab (<c>/fixed</c>): suggestions the owner accepts or dismisses, the month's expected,
///              paid and still-to-come totals, and the table of confirmed fixed payments with their status, an edit dialog
///              (name, category, amount, rhythm) and stop tracking. A suggestion is never accepted automatically.
/// </summary>
public partial class FixedPaymentsPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private FixedPaymentsViewModel? _view;
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis) => _view = new FixedPaymentsViewModel(analysis, Month, Formats, Store.Today);
	#endregion

	#region Event Handlers
	/// <summary>Opens the editor of a fixed payment and sends what it returns.</summary>
	/// <param name="row">The fixed payment to edit.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task EditAsync(FixedPaymentRow row)
	{
		object? result = await Dialogs.OpenAsync<FixedPaymentDialog>($"Edit {row.Name}",
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[nameof(FixedPaymentDialog.Edit)] = row.Edit,
				[nameof(FixedPaymentDialog.CategoryOptions)] = _view!.CategoryGroups.SelectMany(g => g.Options).ToList(),
			},
			new DialogOptions { Width = "460px", CloseDialogOnOverlayClick = false });
		if (result is DecisionRequest request)
		{
			await DecideAsync(request);
		}
	}

	/// <summary>Asks to stop tracking a fixed payment and, when the owner agrees, sends the decision.</summary>
	/// <param name="row">The fixed payment to stop tracking.</param>
	/// <returns>A task that completes when the owner answered and the decision was sent.</returns>
	private async Task StopAsync(FixedPaymentRow row)
	{
		var confirmed = await Dialogs.Confirm(FixedPaymentsViewModel.StopTrackingPrompt, $"Stop tracking {row.Name}",
			new ConfirmOptions { OkButtonText = "Stop tracking", CancelButtonText = "Keep" });
		if (confirmed == true)
		{
			await DecideAsync(FixedPaymentsViewModel.StopTracking(row));
		}
	}
	#endregion
}
