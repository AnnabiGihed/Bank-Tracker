namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The banner of a failed sync (with a link to reconnect) or of a sync that left transactions out (with a
///              button that reads the longest history again). It shows nothing when the last sync was clean.
/// </summary>
public sealed partial class SyncBanner : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the store that holds the workspace.</summary>
	[Inject]
	public required WorkspaceStore Store { get; set; }

	/// <summary>Gets or sets the service that shows notifications.</summary>
	[Inject]
	public required NotificationService Notifications { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets the notice to show, or <see langword="null"/> for none.</summary>
	[Parameter]
	public SyncNotice? Notice { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the banner goes away.</summary>
	private readonly CancellationTokenSource _lifetime = new();

	/// <summary>Whether a full sync is running.</summary>
	private bool _syncing;
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
	/// <summary>Reads the longest history the bank offers and reports a failure.</summary>
	/// <returns>A task that completes when the sync ended.</returns>
	private async Task SyncAllAsync()
	{
		_syncing = true;
		try
		{
			await SyncActions.RunAsync(Store, Notifications, full: true, _lifetime.Token);
		}
		finally
		{
			_syncing = false;
		}
	}
	#endregion
}
