namespace KbcTracker.Components.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Debts tab (<c>/debts</c>, ADR-0010): a card per debt with its progress, status pill, next payment, an
///              expandable schedule and the payments counted ("Not a repayment", "Add a payment"), "Edit plan" and "Remove";
///              "Add debt" for a loan that has no credit; and the "Unplanned" loan credits with "Plan it". The plan form is
///              <see cref="RepaymentPlanDialog"/>; the figures come from <see cref="DebtsViewModel"/>.
/// </summary>
public partial class DebtsPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the service that opens dialogs.</summary>
	[Inject]
	public required DialogService Dialogs { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private DebtsViewModel? _view;

	/// <summary>The debts whose details are open, which stay open when the workspace reloads.</summary>
	private readonly HashSet<string> _open = new(StringComparer.Ordinal);
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis) => _view = new DebtsViewModel(analysis, Formats, Store.Today);
	#endregion

	#region Event Handlers
	/// <summary>Opens or closes the details of a debt.</summary>
	/// <param name="card">The debt.</param>
	private void Toggle(DebtCard card)
	{
		if (!_open.Remove(card.Id))
		{
			_open.Add(card.Id);
		}
	}

	/// <summary>Opens the form for a loan that has no credit and sends what it returns.</summary>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task AddDebtAsync() => await OpenPlanAsync(Dialogs, _view!.PlanForNewDebt());

	/// <summary>Opens the form for an unplanned loan credit and sends what it returns.</summary>
	/// <param name="credit">The credit.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task PlanItAsync(UnplannedCredit credit)
	{
		if (_view!.PlanForCredit(credit.Id) is { } edit)
		{
			await OpenPlanAsync(Dialogs, edit);
		}
	}

	/// <summary>Opens the form of an existing debt and sends what it returns.</summary>
	/// <param name="card">The debt.</param>
	/// <returns>A task that completes when the dialog closed and its decision was sent.</returns>
	private async Task EditPlanAsync(DebtCard card)
	{
		if (_view!.PlanForDebt(card.Id) is { } edit)
		{
			await OpenPlanAsync(Dialogs, edit);
		}
	}

	/// <summary>Asks to stop tracking a debt and, when the owner agrees, sends the decision.</summary>
	/// <param name="card">The debt.</param>
	/// <returns>A task that completes when the owner answered and the decision was sent.</returns>
	private async Task RemoveAsync(DebtCard card)
	{
		var confirmed = await Dialogs.Confirm(DebtsViewModel.RemovePrompt, $"Remove {card.Lender}",
			new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Keep" });
		if (confirmed == true)
		{
			await DecideAsync(card.RemoveRequest);
		}
	}

	/// <summary>Says that the payment the owner picked repays the debt.</summary>
	/// <param name="card">The debt.</param>
	/// <param name="paymentId">The picked payment, or an empty value when cleared.</param>
	/// <returns>A task that completes when the decision was sent.</returns>
	private async Task AddPaymentAsync(DebtCard card, string paymentId)
	{
		if (!string.IsNullOrEmpty(paymentId))
		{
			await DecideAsync(card.AddPaymentRequest(paymentId));
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Gets the style class of a tone.</summary>
	/// <param name="tone">The tone.</param>
	/// <returns>The style class name.</returns>
	private static string ToneClass(PillTone tone) => tone switch
	{
		PillTone.Ok => "ok",
		PillTone.Due => "due",
		PillTone.Bad => "bad",
		_ => "nil",
	};

	/// <summary>Tells whether the details of a debt are open.</summary>
	/// <param name="card">The debt.</param>
	/// <returns><see langword="true"/> when the details are open.</returns>
	private bool IsOpen(DebtCard card) => _open.Contains(card.Id);
	#endregion
}
