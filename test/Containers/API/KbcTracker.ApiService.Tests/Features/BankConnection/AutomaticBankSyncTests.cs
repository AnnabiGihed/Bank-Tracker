using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Domain.Features.BankConnection.Aggregates;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the backend's worker syncs a valid connection by itself, without a user interface or a
///              manual request, and leaves alone a connection whose consent ended or that synced recently, and that
///              setting the interval to zero turns it off.
/// </summary>
/// <param name="fixture">The isolated real-database API host; each test sets the connection it needs.</param>
public sealed class AutomaticBankSyncTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Static Instances
	/// <summary>How long the worker may take: it waits five seconds after start-up, then syncs.</summary>
	private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

	/// <summary>How long a test waits to be sure the worker's first check has passed without a sync.</summary>
	private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(8);
	#endregion

	#region Worker Tests
	/// <summary>Verifies a running backend syncs a valid connection that has never synced and records the outcome.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Worker_ValidConnectionNeverSynced_SyncsWithoutRequest()
	{
		await SetConnectionAsync(validUntil: DateTimeOffset.UtcNow.AddDays(30), lastSync: null);
		var bank = ApiFixture.DownloadingSource();
		using var factory = WorkerFactory(bank, autoSyncHours: 6);
		var started = DateTime.UtcNow;
		var synced = false;
		while (!synced && DateTime.UtcNow - started < Deadline)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500));
			synced = await LastSyncRecordedAsync(factory.Services);
		}

		synced.Should().BeTrue("the worker syncs a connection whose last sync is older than AutoSyncHours");
		await bank.Received().GetTransactionsAsync("current", Arg.Any<DateOnly?>(), Arg.Any<bool>());
	}

	/// <summary>Verifies the worker does not call the bank when the consent ended, the last sync is recent, or the
	/// interval is zero.</summary>
	/// <param name="consentDays">Days until the consent ends; negative when it already ended.</param>
	/// <param name="lastSyncHoursAgo">Hours since the last sync, or a negative value for never.</param>
	/// <param name="autoSyncHours">The configured interval.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData(-1, -1, 6)]
	[InlineData(30, 1, 6)]
	[InlineData(30, -1, 0)]
	public async Task Worker_NothingDue_BankNotCalled(int consentDays, int lastSyncHoursAgo, int autoSyncHours)
	{
		await SetConnectionAsync(DateTimeOffset.UtcNow.AddDays(consentDays),
			lastSyncHoursAgo < 0 ? null : DateTimeOffset.UtcNow.AddHours(-lastSyncHoursAgo));
		var bank = ApiFixture.DownloadingSource();
		using var factory = WorkerFactory(bank, autoSyncHours);
		_ = factory.Services;

		await Task.Delay(FirstCheck);

		await bank.DidNotReceiveWithAnyArgs().GetTransactionsAsync(default!, default, default);
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates a backend whose worker runs with the given interval over the given bank double.</summary>
	private WebApplicationFactory<Program> WorkerFactory(IBankTransactionSource bank, int autoSyncHours) =>
		fixture.CreateFactory("Development", transactionSource: bank)
			.WithWebHostBuilder(builder => builder.UseSetting("AutoSyncHours", autoSyncHours.ToString(System.Globalization.CultureInfo.InvariantCulture)));

	/// <summary>Replaces the stored connection, through a backend whose worker is off.</summary>
	private async Task SetConnectionAsync(DateTimeOffset validUntil, DateTimeOffset? lastSync)
	{
		using var factory = fixture.CreateFactory("Development");
		var scope = factory.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			db.BankSessions.RemoveRange(await db.BankSessions.ToListAsync().ConfigureAwait(false));
			db.BankSessions.Add(SessionInfo.Rehydrate(new BankSessionId("worker-test"), ["current"], DateTimeOffset.UtcNow.AddDays(-1),
				validUntil, lastSync, null, 0));
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Reads whether the current connection has a recorded sync.</summary>
	private static async Task<bool> LastSyncRecordedAsync(IServiceProvider services)
	{
		var scope = services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			return await db.BankSessions.AsNoTracking().AnyAsync(session => session.LastSync != null).ConfigureAwait(false);
		}
	}
	#endregion
}
