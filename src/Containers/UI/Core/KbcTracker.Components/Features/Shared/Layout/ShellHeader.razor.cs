namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The header of every page: when the bank was last read, the warning that the bank access ends, the month
///              selector (previous and next month as links, a drop-down for any month) on the tabs that are about one
///              month, and the Sync button, or a link to connect a bank when none is connected.
/// </summary>
public sealed partial class ShellHeader : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the store that holds the workspace.</summary>
	[Inject]
	public required WorkspaceStore Store { get; set; }

	/// <summary>Gets or sets the navigation manager.</summary>
	[Inject]
	public required NavigationManager Navigation { get; set; }

	/// <summary>Gets or sets the service that shows notifications.</summary>
	[Inject]
	public required NotificationService Notifications { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets what the header shows, or <see langword="null"/> while the workspace is not loaded.</summary>
	[Parameter]
	public ShellViewModel? Shell { get; set; }

	/// <summary>Gets or sets the id of the tab on show, which decides whether the month selector is shown.</summary>
	[Parameter]
	public string CurrentTab { get; set; } = string.Empty;
	#endregion

	#region Fields
	/// <summary>Cancelled when the header goes away, so a running sync stops with it.</summary>
	private readonly CancellationTokenSource _lifetime = new();

	/// <summary>Whether a sync is running.</summary>
	private bool _syncing;
	#endregion

	#region Properties
	/// <summary>Gets the accessible name of the month drop-down.</summary>
	private static IReadOnlyDictionary<string, object> MonthInputAttributes { get; } = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-label"] = "Month" };

	/// <summary>Gets a value indicating whether the tab on show is about one month.</summary>
	private bool ShowsMonths => ShellViewModel.ShowsMonthSelector(CurrentTab);
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		_lifetime.Cancel();
		_lifetime.Dispose();
		GC.SuppressFinalize(this);
	}
	#endregion

	#region Event Handlers
	/// <summary>Opens the same tab for the month the owner picked.</summary>
	/// <param name="value">The picked month as <c>yyyy-MM</c>.</param>
	private void OnMonthChanged(object value)
	{
		if (value is string month && month.Length > 0)
		{
			Navigation.NavigateTo(MonthUri(month));
		}
	}

	/// <summary>Syncs with the bank and tells the owner how it went.</summary>
	/// <returns>A task that completes when the sync ended.</returns>
	private async Task SyncAsync()
	{
		_syncing = true;
		try
		{
			await SyncActions.RunAsync(Store, Notifications, full: false, _lifetime.Token);
		}
		finally
		{
			_syncing = false;
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the address of the current page for another month.</summary>
	private string MonthUri(string month) => Navigation.GetUriWithQueryParameter("m", month);
	#endregion
}
