using KbcTracker;

var builder = WebApplication.CreateBuilder(args);

var ebOptions = builder.Configuration.GetSection("EnableBanking").Get<EnableBankingOptions>() ?? new();
Categories.Rules = builder.Configuration.GetSection("Categories").Get<Dictionary<string, string[]>>() ?? [];
Categories.FixedRules = builder.Configuration.GetSection("FixedPayments").Get<List<FixedRule>>() ?? [];

builder.Services.AddSingleton(ebOptions);
builder.Services.AddSingleton<Store>();
builder.Services.AddHttpClient<EnableBankingClient>();

var app = builder.Build();
var pendingStates = new HashSet<string>();

// appsettings.json is reloaded on change → re-read rules on each request, so edits apply without restarting
app.Use(async (ctx, next) =>
{
    Categories.Rules = app.Configuration.GetSection("Categories").Get<Dictionary<string, string[]>>() ?? [];
    Categories.FixedRules = app.Configuration.GetSection("FixedPayments").Get<List<FixedRule>>() ?? [];
    await next();
});

// ---------- pages (one per tab; ?m=yyyy-MM selects the month) ----------
Analysis Analyse(Store store) => new(store.LoadTransactions(), Categories.Rules, store.LoadUserRules());
string Month(string? m, Analysis a) => m is not null && a.AvailableMonths.Contains(m) ? m : a.AvailableMonths.FirstOrDefault() ?? $"{DateTime.Today:yyyy-MM}";
IResult Html(string html) => Results.Content(html, "text/html");

app.MapGet("/", (string? m) => Results.Redirect("/overview" + (m is null ? "" : "?m=" + m)));
app.MapGet("/overview", (string? m, Store store) => { var a = Analyse(store); return Html(Pages.Overview(a, Month(m, a), store.LoadSession())); });
app.MapGet("/fixed", (string? m, Store store) => { var a = Analyse(store); return Html(Pages.Fixed(a, Month(m, a), store.LoadSession(), store.LoadUserRules())); });
app.MapGet("/transactions", (string? m, string? cat, Store store) => { var a = Analyse(store); return Html(Pages.Transactions(a, Month(m, a), store.LoadSession(), cat)); });
app.MapGet("/categories", (string? m, Store store) => { var a = Analyse(store); return Html(Pages.CategoriesPage(a, Month(m, a), store.LoadSession())); });
app.MapGet("/settings", (string? m, Store store) => { var a = Analyse(store); return Html(Pages.Settings(a, Month(m, a), store.LoadSession(), store.LoadUserRules())); });

// ---------- your corrections from the UI ----------
app.MapPost("/api/rules", (RuleChange c, Store store) =>
{
    var r = store.LoadUserRules();
    switch (c.Action)
    {
        case "recurring": r.NotRecurring.Remove(c.Key); r.Recurring.Add(c.Key); break;
        case "notrecurring": r.Recurring.Remove(c.Key); r.NotRecurring.Add(c.Key); break;
        case "restore": r.NotRecurring.Remove(c.Key); r.Recurring.Remove(c.Key); break;
        case "category" when !string.IsNullOrWhiteSpace(c.Value): r.Categories[c.Key] = c.Value!; break;
        case "name" when !string.IsNullOrWhiteSpace(c.Value): r.Names[c.Key] = c.Value!.Trim(); break;
        case "reset": r = new UserRules(); break;
        default: return Results.BadRequest();
    }
    store.SaveUserRules(r);
    return Results.Ok();
});

// 1. Pick the bank (KBC Brussels)
app.MapGet("/banks", async (EnableBankingClient eb) =>
    Results.Content(Pages.Banks(await eb.GetBanksAsync()), "text/html"));

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
    return Results.Redirect("/sync?full=true");
});

// 4. Download transactions (run it whenever you want fresh data)
app.MapGet("/sync", async (bool? full, string? back, EnableBankingClient eb, Store store, ILogger<Program> log) =>
{
    var session = store.LoadSession();
    if (session is null) return Results.Redirect("/banks");

    // First sync / full re-sync: ask for the longest history the bank allows (strategy=longest).
    // Daily syncs: last 30 days is enough — Merge() dedups and keeps older history we already stored.
    var full_ = full == true || store.LoadTransactions().Count == 0;
    var from = full_
        ? DateOnly.FromDateTime(DateTime.Today.AddMonths(-ebOptions.HistoryMonths))
        : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));

    var added = 0;
    foreach (var uid in session.AccountUids)
    {
        try
        {
            var raw = await eb.GetTransactionsAsync(uid, from, longest: full_);
            added += store.Merge(raw.Select(t => Store.Map(uid, t)));
        }
        catch (InvalidOperationException ex) when (!full_)
        {
            log.LogWarning("Incremental sync refused ({Msg}), retrying with strategy=longest", ex.Message);
            var raw = await eb.GetTransactionsAsync(uid, null, longest: true);
            added += store.Merge(raw.Select(t => Store.Map(uid, t)));
        }
    }
    session.LastSync = DateTimeOffset.UtcNow;
    store.SaveSession(session);
    log.LogInformation("Sync done, {Added} new transactions", added);
    return Results.Redirect(back is not null && back.StartsWith('/') && !back.StartsWith("//") ? back : "/");
});

app.MapGet("/export.csv", (Store store) =>
    Results.File(System.Text.Encoding.UTF8.GetBytes(Store.ToCsv(Analyse(store))), "text/csv", "kbc-transactions.csv"));

app.Run();

record RuleChange(string Action, string Key, string? Value);
