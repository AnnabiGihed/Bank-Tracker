namespace KbcTracker.Web.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : While the web host runs, starts a sync every few hours so nothing falls out of KBC's 90-day
///              window.
///
///              Checks every 30 minutes whether the last sync is older than the <c>AutoSyncHours</c> setting
///              (0 switches it off). The sync logic itself lives in <see cref="SyncService"/> in the shared core.
/// </summary>
public sealed partial class AutoSync : BackgroundService
{
	#region Fields
	private readonly IServiceProvider _services;
	private readonly IConfiguration _config;
	private readonly ILogger<AutoSync> _log;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="AutoSync"/>.
	/// </summary>
	/// <param name="services">Root provider, used to create a scope per check.</param>
	/// <param name="config">Configuration holding <c>AutoSyncHours</c>.</param>
	/// <param name="log">Logger for automatic syncs.</param>
	public AutoSync(IServiceProvider services, IConfiguration config, ILogger<AutoSync> log)
	{
		_services = services;
		_config = config;
		_log = log;
	}
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var hours = _config.GetValue("AutoSyncHours", 6);
		if (hours <= 0)
		{
			return;
		}

		await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
		while (!stoppingToken.IsCancellationRequested)
		{
			await SyncIfDueAsync(hours).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken).ConfigureAwait(false);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Starts a sync when the user is connected and the last sync is older than <paramref name="hours"/>.
	/// </summary>
	private async Task SyncIfDueAsync(int hours)
	{
		using var scope = _services.CreateScope();
		var session = scope.ServiceProvider.GetRequiredService<IStore>().LoadSession();
		var isDue = session is not null && (session.LastSync is null || session.LastSync < DateTimeOffset.UtcNow.AddHours(-hours));
		if (isDue)
		{
			LogAutomaticSync();
			await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Logs that an automatic sync starts.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Information, Message = "Automatic sync")]
	private partial void LogAutomaticSync();
	#endregion
}
