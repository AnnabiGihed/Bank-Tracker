using KbcTracker;

var builder = WebApplication.CreateBuilder(args);

var ebOptions = builder.Configuration.GetSection("EnableBanking").Get<EnableBankingOptions>() ?? new();
Categories.Rules = builder.Configuration.GetSection("Categories").Get<Dictionary<string, string[]>>() ?? [];

builder.Services.AddSingleton(ebOptions);
builder.Services.AddSingleton<Store>();
builder.Services.AddHttpClient<EnableBankingClient>();

var app = builder.Build();
var pendingStates = new HashSet<string>();

// Dashboard
app.MapGet("/", (Store store) =>
{
    var txns = store.LoadTransactions();
    return Results.Content(Dashboard.Render(new Analysis(txns, Categories.Rules), txns, store.LoadSession()), "text/html");
});

// 1. Pick the bank (KBC Brussels)
app.MapGet("/banks", async (EnableBankingClient eb) =>
    Results.Content(Dashboard.Banks(await eb.GetBanksAsync()), "text/html"));

// 2. Start consent → redirects to KBC login (itsme / card reader)
app.MapGet("/connect", async (string name, string country, EnableBankingClient eb) =>
{
    var state = Guid.NewGuid().ToString();
    pendingStates.Add(state);
    return Results.Redirect(await eb.StartAuthAsync(name, country, state));
});

// 3. Bank redirects back here with ?code=...
app.MapGet("/callback", async (string? code, string? state, string? error, string? error_description, EnableBankingClient eb, Store store) =>
{
    if (error is not null) return Results.Content($"Bank authorisation failed: {error} {error_description}", "text/plain");
    if (code is null || state is null || !pendingStates.Remove(state)) return Results.BadRequest("Unknown or expired authorisation request — start again from /banks.");

    var (sessionId, uids, _) = await eb.CreateSessionAsync(code);
    store.SaveSession(new SessionInfo
    {
        SessionId = sessionId,
        AccountUids = uids,
        ConnectedAt = DateTimeOffset.UtcNow,
        ValidUntil = DateTimeOffset.UtcNow.AddDays(ebOptions.ConsentDays)
    });
    return Results.Redirect("/sync");
});

// 4. Download transactions (run it whenever you want fresh data)
app.MapGet("/sync", async (EnableBankingClient eb, Store store, ILogger<Program> log) =>
{
    var session = store.LoadSession();
    if (session is null) return Results.Redirect("/banks");

    // first sync: go back as far as allowed; later syncs: last 30 days is enough (merge dedups)
    var from = store.LoadTransactions().Count == 0
        ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-ebOptions.HistoryMonths))
        : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));

    var added = 0;
    foreach (var uid in session.AccountUids)
    {
        try
        {
            var raw = await eb.GetTransactionsAsync(uid, from);
            added += store.Merge(raw.Select(t => Store.Map(uid, t)));
        }
        catch (InvalidOperationException ex) when (from < DateOnly.FromDateTime(DateTime.Today.AddDays(-89)))
        {
            // Some banks only return 90 days of history after the first consent → retry with 89 days
            log.LogWarning("Full history refused ({Msg}), retrying with 89 days", ex.Message);
            var raw = await eb.GetTransactionsAsync(uid, DateOnly.FromDateTime(DateTime.Today.AddDays(-89)));
            added += store.Merge(raw.Select(t => Store.Map(uid, t)));
        }
    }
    session.LastSync = DateTimeOffset.UtcNow;
    store.SaveSession(session);
    log.LogInformation("Sync done, {Added} new transactions", added);
    return Results.Redirect("/");
});

app.MapGet("/export.csv", (Store store) =>
    Results.File(System.Text.Encoding.UTF8.GetBytes(store.ToCsv(store.LoadTransactions())), "text/csv", "kbc-transactions.csv"));

app.Run();
