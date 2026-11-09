using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var ebOptions = builder.Configuration.GetSection("EnableBanking").Get<EnableBankingOptions>() ?? new();
CategorySeed.Keywords = builder.Configuration.GetSection("Categories").Get<Dictionary<string, string[]>>() ?? new Dictionary<string, string[]>(StringComparer.Ordinal);

builder.Services.AddSingleton(ebOptions);
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<IStore>(sp => sp.GetRequiredService<Store>());
builder.Services.AddSingleton(new SyncSettings(ebOptions.HistoryMonths));
builder.Services.AddHttpClient<EnableBankingClient>();
builder.Services.AddTransient<IBankTransactionSource, EnableBankingTransactionSource>();
builder.Services.AddTransient<SyncService>();
builder.Services.AddHostedService<AutoSync>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// `dotnet KbcTracker.Web.dll --sync` → sync once and exit (used by the daily scheduled task)
if (args.Contains("--sync"))
{
	using var scope = app.Services.CreateScope();
	var (added, error) = await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAsync();
	Console.WriteLine(error ?? $"OK, {added} new transactions");
	return error is null ? 0 : 1;
}

var pendingStates = new HashSet<string>();

Analysis Analyse(Store s) => new(s.LoadTransactions(), s.LoadCategories(CategorySeed.Keywords), s.LoadUserData());
string Month(string? m, Analysis a) => m is not null && a.AvailableMonths.Contains(m) ? m : a.AvailableMonths.FirstOrDefault() ?? $"{DateTime.Today:yyyy-MM}";
IResult HtmlResult(string html) => Results.Content(html, "text/html");

// ---------- pages (one per tab; ?m=yyyy-MM selects the month) ----------
app.MapGet("/", () => Results.Redirect("/overview"));
app.MapGet("/overview", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(OverviewPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/fixed", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(FixedPaymentsPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/transactions", (string? m, string? cat, string? q, Store s) => { var a = Analyse(s); return HtmlResult(TransactionsPage.Render(a, Month(m, a), s.LoadSession(), cat, q)); });
app.MapGet("/review", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(ReviewPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/categories", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(CategoriesPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/settings", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(SettingsPage.Render(a, Month(m, a), s.LoadSession(), s.Dir)); });

// ---------- everything you change in the UI ----------
app.MapPost("/api", (ApiRequest r, Store store) =>
{
	var user = store.LoadUserData();
	object? result = null;
	switch (r.Action)
	{
		// fixed payments
		case "accept":
		{
			var a = Analyse(store);
			var sug = a.Suggestions.FirstOrDefault(x => x.Signature == r.Key);
			if (sug is null) return Results.NotFound();
			user.Recurring.Add(Analysis.ToDefinition(sug));
			break;
		}
		case "dismiss":
			user.DismissedSuggestions.Add(r.Key!);
			break;
		case "mark":
		{
			var g = Analyse(store).GroupFor(r.Key!);
			user.DismissedSuggestions.Remove(g.Signature);
			user.Recurring.Add(Analysis.ToDefinition(g));
			break;
		}
		case "updateFixed":
		{
			var d = user.Recurring.First(x => x.Id == r.Id);
			if (!string.IsNullOrWhiteSpace(r.Name)) d.Name = r.Name.Trim();
			if (r.CategoryId is not null) d.CategoryId = r.CategoryId;
			if (r.Amount is > 0) d.Amount = r.Amount.Value;
			if (r.Cadence is > 0) d.CadenceDays = r.Cadence.Value;
			break;
		}
		case "removeFixed":
		{
			var d = user.Recurring.First(x => x.Id == r.Id);
			user.Recurring.Remove(d);
			user.DismissedSuggestions.Add(Analysis.Signature(d.SeriesKeys));
			break;
		}

		// categories of payments
		case "payeeCategory":   // applies to every payment of this payee; returns what it was before (for "only this one")
		{
			result = user.Payees.TryGetValue(r.Key!, out var prev) ? new { prev.CategoryId, prev.Confirmed } : null;
			user.Payees[r.Key!] = new PayeeAssignment { CategoryId = r.CategoryId, Confirmed = true };
			break;
		}
		case "txnOnly":         // undo the payee change and set the category on one transaction only
		{
			if (r.Restore is null) user.Payees.Remove(r.Key!); else user.Payees[r.Key!] = r.Restore;
			user.TxnCategories[r.Id!] = r.CategoryId!;
			break;
		}
		case "confirm":         // accept the automatic category for these payees
		{
			var a = Analyse(store);
			foreach (var key in r.Keys ?? [r.Key!])
			{
				var cur = a.Items.FirstOrDefault(i => i.SeriesKey == key);
				if (cur is not null) user.Payees[key] = new PayeeAssignment { CategoryId = cur.Category.Id, Confirmed = true };
			}
			break;
		}
		case "rename":
			user.PayeeNames[r.Key!] = r.Name!.Trim();
			break;

		// category definitions
		case "saveCategory":
		{
			var cats = store.LoadCategories(CategorySeed.Keywords);
			var c = cats.FirstOrDefault(x => x.Id == r.Id);
			if (c is null)
			{
				var id = Store.Slug(r.Name!);
				while (cats.Any(x => x.Id == id)) id += "-2";
				c = new Category { Id = id };
				cats.Add(c);
			}
			c.Name = r.Name!.Trim();
			c.Type = Enum.TryParse<CategoryType>(r.Type, out var t) ? t : CategoryType.Expense;
			c.Color = r.Color ?? c.Color;
			c.Keywords = (r.Keywords ?? "").Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
			store.SaveCategories(cats);
			result = new { c.Id };
			break;
		}
		case "deleteCategory":
		{
			var cats = store.LoadCategories(CategorySeed.Keywords);
			var c = cats.FirstOrDefault(x => x.Id == r.Id);
			if (c is null || c.Id == "other") return Results.BadRequest("This category can't be deleted.");
			cats.Remove(c);
			store.SaveCategories(cats);
			// its payees go back to automatic categorisation (and show up in "To review")
			foreach (var k in user.Payees.Where(kv => kv.Value.CategoryId == c.Id).Select(kv => kv.Key).ToList()) user.Payees.Remove(k);
			foreach (var k in user.TxnCategories.Where(kv => kv.Value == c.Id).Select(kv => kv.Key).ToList()) user.TxnCategories.Remove(k);
			foreach (var d in user.Recurring.Where(d => d.CategoryId == c.Id)) d.CategoryId = null;
			break;
		}
		default: return Results.BadRequest();
	}
	store.SaveUserData(user);
	return Results.Ok(result);
});

// ---------- bank connection ----------
app.MapGet("/banks", async (EnableBankingClient eb) => HtmlResult(BankPickerPage.Render([.. await eb.GetBanksAsync()])));

app.MapGet("/connect", async (string name, string country, EnableBankingClient eb) =>
{
	var state = Guid.NewGuid().ToString();
	pendingStates.Add(state);
	return Results.Redirect(await eb.StartAuthAsync(name, country, state));
});

app.MapGet("/callback", async (string? code, string? state, string? error, string? error_description, EnableBankingClient eb, Store store) =>
{
	if (error is not null) return Results.Content($"Bank authorisation failed: {error} {error_description}", "text/plain");
	if (code is null || state is null || !pendingStates.Remove(state)) return Results.BadRequest("Unknown or expired authorisation request — start again from /banks.");

	var (sessionId, uids, _) = await eb.CreateSessionAsync(code);
	var old = store.LoadSession();
	store.SaveSession(new SessionInfo
	{
		SessionId = sessionId,
		AccountUids = uids,
		ConnectedAt = DateTimeOffset.UtcNow,
		ValidUntil = DateTimeOffset.UtcNow.AddDays(ebOptions.ConsentDays),
		LastSync = old?.LastSync
	});
	return Results.Redirect("/sync?full=true&back=/settings");
});

app.MapGet("/sync", async (bool? full, string? back, SyncService sync) =>
{
	await sync.SyncAsync(full == true);
	return Results.Redirect(back is not null && back.StartsWith('/') && !back.StartsWith("//") ? back : "/");
});

app.MapGet("/export.csv", (Store s) =>
	Results.File(System.Text.Encoding.UTF8.GetBytes(Store.ToCsv(Analyse(s))), "text/csv", "kbc-transactions.csv"));

app.Run();
return 0;
