namespace KbcTracker.Components.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The content of the notification shown after a category was set for a whole payee: a button that limits
///              the change to the one payment that was edited. It lives in a notification, which outlives the page that
///              raised it, so it takes the action as a plain delegate instead of an event of the page.
/// </summary>
public partial class OnlyThisOneNotice : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets what the button does: it sends the decision that limits the change to one payment.</summary>
	[EditorRequired]
	[Parameter]
	public required Func<Task> Apply { get; set; }
	#endregion

	#region Fields
	/// <summary>Whether the action is running.</summary>
	private bool _busy;

	/// <summary>Whether the action was done, so the button is replaced by a confirmation and cannot be clicked twice.</summary>
	private bool _applied;
	#endregion

	#region Event Handlers
	/// <summary>Runs the action once.</summary>
	/// <returns>A task that completes when the action ended.</returns>
	private async Task ApplyAsync()
	{
		_busy = true;
		try
		{
			await Apply();
			_applied = true;
		}
		finally
		{
			_busy = false;
		}
	}
	#endregion
}
