namespace KbcTracker.Components.Features.Settings;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Settings tab (<c>/settings</c>): the bank connection (accounts, when the access ends, the last sync),
///              Sync now, Sync full history and Reconnect, and the stored history with a link to the CSV export. Reconnect
///              goes through <see cref="IBankConnectionLauncher"/>: where the host cannot open the bank (the Android app)
///              the button is off and the launcher's own message says why. The CSV link appears only when the host sets
///              <see cref="KbcTrackerUiOptions.ExportCsvPath"/>.
/// </summary>
public partial class SettingsPage : TabPageBase
{
	#region Injected Services
	/// <summary>Gets or sets the host's way to open the bank's consent page.</summary>
	[Inject]
	public required IBankConnectionLauncher Launcher { get; set; }

	/// <summary>Gets or sets what the host tells the components about itself.</summary>
	[Inject]
	public required KbcTrackerUiOptions Options { get; set; }
	#endregion

	#region Fields
	/// <summary>What the tab shows, or <see langword="null"/> until the workspace is loaded.</summary>
	private SettingsViewModel? _view;

	/// <summary>Whether a sync is running.</summary>
	private bool _syncing;
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override void Rebuild(Analysis analysis) => _view = new SettingsViewModel(analysis, Store.Session, Formats);
	#endregion

	#region Event Handlers
	/// <summary>Syncs with the bank and tells the owner how it went.</summary>
	/// <param name="full">Whether to read the longest history the bank offers.</param>
	/// <returns>A task that completes when the sync ended.</returns>
	private async Task SyncAsync(bool full)
	{
		_syncing = true;
		try
		{
			await SyncActions.RunAsync(Store, Notifications, full, Cancel);
		}
		finally
		{
			_syncing = false;
		}
	}

	/// <summary>Opens the bank picker, where the connection is started.</summary>
	private void GoToBanks() => Navigation.NavigateTo(TabRoutes.Banks);
	#endregion
}
