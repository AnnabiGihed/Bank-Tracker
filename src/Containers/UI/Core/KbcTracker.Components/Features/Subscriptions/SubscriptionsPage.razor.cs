namespace KbcTracker.Components.Features.Subscriptions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Subscriptions tab (<c>/subscriptions</c>): suggestions of recurring card payments the owner accepts
///              or dismisses, the totals per month and per year, and the table of accepted subscriptions with their
///              cost, last and next charge and status, an edit dialog and stop tracking. It sends the same decisions as
///              the Fixed payments tab; a suggestion is never accepted automatically.
/// </summary>
public partial class SubscriptionsPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private SubscriptionsViewModel? _view;
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis) => _view = new SubscriptionsViewModel(analysis, Month, Formats, Store.Today);
	#endregion

	#region Event Handlers
	/// <summary>Opens the editor of a subscription and sends what it returns.</summary>
	/// <param name="row">The subscription to edit.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task EditAsync(SubscriptionRow row)
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

	/// <summary>Asks to stop tracking a subscription and, when the owner agrees, sends the decision.</summary>
	/// <param name="row">The subscription to stop tracking.</param>
	/// <returns>A task that completes when the owner answered and the decision was sent.</returns>
	private async Task StopAsync(SubscriptionRow row)
	{
		var confirmed = await Dialogs.Confirm(SubscriptionsViewModel.StopTrackingPrompt, $"Stop tracking {row.Name}",
			new ConfirmOptions { OkButtonText = "Stop tracking", CancelButtonText = "Keep" });
		if (confirmed == true)
		{
			await DecideAsync(SubscriptionsViewModel.StopTracking(row));
		}
	}
	#endregion
}
