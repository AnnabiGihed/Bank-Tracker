using KbcTracker.Application.Features.BankConnection.Services;
using KbcTracker.Domain.Features.BankConnection.Repositories;

namespace KbcTracker.ApiService.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Keeps automatic bank downloads on the backend independently of whether a UI is running.
/// </summary>
/// <param name="scopes">Creates a separate persistence scope for each check.</param>
/// <param name="clock">The clock used to check the stored sync time.</param>
/// <param name="configuration">The configured sync interval; zero disables polling.</param>
/// <param name="logger">Records safe worker failures without provider data.</param>
public sealed partial class AutomaticBankSync(IServiceScopeFactory scopes, TimeProvider clock, IConfiguration configuration,
	ILogger<AutomaticBankSync> logger) : BackgroundService
{
	#region Overrides
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var hours = configuration.GetValue("AutoSyncHours", 6);
		if (hours <= 0)
		{
			return;
		}

		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30), clock);
		await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken).ConfigureAwait(false);
		do
		{
			await CheckAsync(hours, stoppingToken).ConfigureAwait(false);
		}
		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
	}
	#endregion

	#region Private Helpers
	/// <summary>Checks the current consent before starting the shared, database-locked sync workflow.</summary>
	private async Task CheckAsync(int hours, CancellationToken cancellationToken)
	{
		try
		{
			var scope = scopes.CreateAsyncScope();
			await using (scope.ConfigureAwait(false))
			{
				var session = await scope.ServiceProvider.GetRequiredService<IBankSessionRepository>().GetCurrentAsync(cancellationToken).ConfigureAwait(false);
				if (session is not null && session.ValidUntil > clock.GetUtcNow()
					&& (session.LastSync is null || session.LastSync < clock.GetUtcNow().AddHours(-hours)))
				{
					await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Provider exceptions can contain bank data; the worker records the failure kind rather than its contents.
			LogCheckFailed(exception.GetType().Name);
		}
	}

	/// <summary>Records a failed background check without connection strings or bank response bodies.</summary>
	[LoggerMessage(Level = LogLevel.Error, Message = "Automatic bank sync check failed ({FailureKind}); retrying at the next interval.")]
	private partial void LogCheckFailed(string failureKind);
	#endregion
}
