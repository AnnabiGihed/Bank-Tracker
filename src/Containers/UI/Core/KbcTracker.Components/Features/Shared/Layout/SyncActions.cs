namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs a sync for the components that offer one (the header, the sync banner and Settings) and tells the
///              owner how it went, so every Sync button behaves the same.
/// </summary>
public static class SyncActions
{
	#region Constants
	/// <summary>How long the success message stays on screen, in milliseconds.</summary>
	private const double SuccessDuration = 4000;
	#endregion

	#region Public Methods
	/// <summary>Syncs with the bank through the backend and shows a success or an error notification.</summary>
	/// <param name="store">The store that sends the request and reloads the workspace.</param>
	/// <param name="notifications">The notification service of the host.</param>
	/// <param name="full">Whether to read the longest history the bank offers.</param>
	/// <param name="cancellationToken">Cancels the wait when the caller goes away; the backend may still finish.</param>
	/// <returns>A task that completes when the sync ended, or the caller went away.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="notifications"/> is <see langword="null"/>.</exception>
	public static async Task RunAsync(WorkspaceStore store, NotificationService notifications, bool full, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(notifications);
		try
		{
			var result = await (full ? store.SyncFullAsync(cancellationToken) : store.SyncAsync(cancellationToken)).ConfigureAwait(true);
			if (result.Problem is { } problem)
			{
				ProblemNotices.Report(notifications, problem);
				return;
			}

			notifications.Notify(new NotificationMessage
			{
				Severity = NotificationSeverity.Success,
				Summary = "Sync complete",
				Detail = $"{result.Value?.Added ?? 0} new transaction(s).",
				Duration = SuccessDuration,
			});
		}
		catch (OperationCanceledException)
		{
			// The caller went away while the sync ran; the backend finishes it on its own.
		}
	}
	#endregion
}
