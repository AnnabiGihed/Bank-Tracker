namespace KbcTracker;

/// <summary>Downloads new transactions from KBC (via Enable Banking) and adds them to the permanent history.</summary>
public sealed class SyncService(EnableBankingClient eb, Store store, EnableBankingOptions options, ILogger<SyncService> log)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<(int Added, string? Error)> SyncAsync(bool full = false)
    {
        var session = store.LoadSession();
        if (session is null) return (0, "Not connected to the bank yet.");
        await Gate.WaitAsync();
        try
        {
            // First sync / full re-sync: the longest history the bank allows (strategy=longest).
            // Regular syncs: last 30 days is enough — everything older is already stored and never removed.
            full = full || store.LoadTransactions().Count == 0;
            var from = full
                ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-options.HistoryMonths))
                : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));

            var added = 0;
            foreach (var uid in session.AccountUids)
            {
                List<System.Text.Json.Nodes.JsonNode> raw;
                try { raw = await eb.GetTransactionsAsync(uid, from, longest: full); }
                catch (InvalidOperationException ex) when (!full)
                {
                    log.LogWarning("Incremental sync refused ({Msg}), retrying with strategy=longest", ex.Message);
                    raw = await eb.GetTransactionsAsync(uid, null, longest: true);
                }
                added += store.Merge(raw.Select(t => Store.Map(uid, t)));
            }
            session.LastSync = DateTimeOffset.UtcNow;
            session.LastError = null;
            store.SaveSession(session);
            log.LogInformation("Sync done, {Added} new transactions", added);
            return (added, null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Sync failed");
            session.LastError = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
            store.SaveSession(session);
            return (0, session.LastError);
        }
        finally { Gate.Release(); }
    }
}

/// <summary>While the app runs, syncs every few hours so nothing falls out of KBC's 90-day window.</summary>
public sealed class AutoSync(IServiceProvider services, IConfiguration config, ILogger<AutoSync> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var hours = config.GetValue("AutoSyncHours", 6);
        if (hours <= 0) return;
        await Task.Delay(TimeSpan.FromSeconds(5), stop);
        while (!stop.IsCancellationRequested)
        {
            using (var scope = services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<Store>();
                var s = store.LoadSession();
                if (s is not null && (s.LastSync is null || s.LastSync < DateTimeOffset.UtcNow.AddHours(-hours)))
                {
                    log.LogInformation("Automatic sync");
                    await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAsync();
                }
            }
            await Task.Delay(TimeSpan.FromMinutes(30), stop);
        }
    }
}
