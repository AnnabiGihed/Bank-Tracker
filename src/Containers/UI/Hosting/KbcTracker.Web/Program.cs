using System.Globalization;
using System.Text.Json.Serialization;
using Pivot.Framework.Domain.Shared;

var builder = WebApplication.CreateBuilder(args);

var ebOptions = builder.Configuration.GetSection("EnableBanking").Get<EnableBankingOptions>() ?? new();
CategorySeed.Keywords = builder.Configuration.GetSection("Categories").Get<Dictionary<string, string[]>>() ?? new Dictionary<string, string[]>(StringComparer.Ordinal);

builder.Services.AddSingleton(ebOptions);
builder.Services.AddSingleton(TimeProvider.System);
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
	var skipped = 0;
	try
	{
		// The error line never shows the left-out count, so the session is read only after a successful sync.
		if (error is null)
		{
			skipped = scope.ServiceProvider.GetRequiredService<Store>().LoadSession()?.SkippedTransactions ?? 0;
		}
	}
	catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
	{
		// The count is only extra information: the outcome line is still printed, and this says the count is unknown.
		await Console.Error.WriteLineAsync($"Left-out count unavailable: {ex.Message}").ConfigureAwait(false);
	}

	Console.WriteLine(SyncReport.Describe(added, error, skipped));
	return error is null ? 0 : 1;
}

var pendingStates = new HashSet<string>();

Analysis Analyse(Store s) => new(s.LoadTransactions(), s.LoadCategories(CategorySeed.Keywords), s.LoadUserData());
string Month(string? m, Analysis a) => m is not null && a.AvailableMonths.Contains(m) ? m : a.AvailableMonths.FirstOrDefault() ?? $"{DateTime.Today:yyyy-MM}";
IResult HtmlResult(string html) => Results.Content(html, "text/html");
CategoryId? CategoryIdOrNull(string? id) => string.IsNullOrWhiteSpace(id) ? null : new CategoryId(id);

// ---------- pages (one per tab; ?m=yyyy-MM selects the month) ----------
app.MapGet("/", () => Results.Redirect("/overview"));
app.MapGet("/overview", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(OverviewPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/fixed", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(FixedPaymentsPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/transactions", (string? m, string? cat, string? q, Store s) => { var a = Analyse(s); return HtmlResult(TransactionsPage.Render(a, Month(m, a), s.LoadSession(), cat, q)); });
app.MapGet("/review", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(ReviewPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/categories", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(CategoriesPage.Render(a, Month(m, a), s.LoadSession())); });
app.MapGet("/settings", (string? m, Store s) => { var a = Analyse(s); return HtmlResult(SettingsPage.Render(a, Month(m, a), s.LoadSession(), s.Dir)); });

// ---------- everything you change in the UI ----------
char[] keywordSeparators = [',', '\n'];

// Each action names the fields it needs in its case guard; a request without them falls through to the default
// branch and is answered 400, instead of failing inside the domain.
app.MapPost("/api", (ApiRequest r, Store store, TimeProvider clock) =>
{
	var user = store.LoadUserData();
	object? result = null;
	switch (r.Action)
	{
		// fixed payments
		case "accept" when Has(r.Key, out var signature):
		{
			var suggestion = Analyse(store).Suggestions.FirstOrDefault(x => string.Equals(x.Signature, signature, StringComparison.Ordinal));
			if (suggestion is null)
			{
				return Results.NotFound("This suggestion no longer exists.");
			}

			var accepted = Analysis.ToDefinition(suggestion, RecurringPaymentId.New(), clock.GetUtcNow());
			if (accepted.IsFailure)
			{
				return Results.BadRequest(accepted.Error.Message);
			}

			user.AcceptFixedPayment(accepted.Value);
			break;
		}

		case "dismiss" when Has(r.Key, out var signature):
			user.DismissSuggestion(signature);
			break;
		case "mark" when Has(r.Key, out var seriesKey):
		{
			var analysis = Analyse(store);
			if (!analysis.Items.Any(i => i.T.Amount < 0 && string.Equals(i.SeriesKey, seriesKey, StringComparison.Ordinal)))
			{
				return Results.NotFound("This payee has no payments to mark.");
			}

			var marked = Analysis.ToDefinition(analysis.GroupFor(seriesKey), RecurringPaymentId.New(), clock.GetUtcNow());
			if (marked.IsFailure)
			{
				return Results.BadRequest(marked.Error.Message);
			}

			user.AcceptFixedPayment(marked.Value);
			break;
		}

		case "updateFixed" when Has(r.Id, out var id):
		{
			var updated = user.UpdateFixedPayment(new RecurringPaymentId(id), r.Name, CategoryIdOrNull(r.CategoryId), r.Amount, r.Cadence);
			if (updated.IsFailure)
			{
				return Results.NotFound(updated.Error.Message);
			}

			break;
		}

		case "removeFixed" when Has(r.Id, out var id):
		{
			var removed = user.RemoveFixedPayment(new RecurringPaymentId(id));
			if (removed.IsFailure)
			{
				return Results.NotFound(removed.Error.Message);
			}

			break;
		}

		// categories of payments: "payeeCategory" applies to every payment of the payee and returns what it was
		// before, which "txnOnly" sends back to undo it and set the category on one transaction only.
		case "payeeCategory" when Has(r.Key, out var seriesKey):
			result = user.Payees.TryGetValue(seriesKey, out var previous) ? PayeeAssignmentDocument.From(previous) : null;
			user.AssignPayeeCategory(seriesKey, CategoryIdOrNull(r.CategoryId));
			break;
		case "txnOnly" when Has(r.Key, out var seriesKey) && Has(r.Id, out var txnId) && Has(r.CategoryId, out var categoryId):
			user.RestorePayeeCategory(seriesKey, r.Restore?.ToDomain());
			user.AssignTransactionCategory(new TxnId(txnId), new CategoryId(categoryId));
			break;
		case "confirm":
		{
			var analysis = Analyse(store);
			var keys = r.Keys ?? (Has(r.Key, out var single) ? [single] : []);
			foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
			{
				var current = analysis.Items.FirstOrDefault(i => string.Equals(i.SeriesKey, key, StringComparison.Ordinal));
				if (current is not null)
				{
					user.AssignPayeeCategory(key, current.Category.Id);
				}
			}

			break;
		}

		case "rename" when Has(r.Key, out var seriesKey):
		{
			var renamed = user.RenamePayee(seriesKey, r.Name);
			if (renamed.IsFailure)
			{
				return Results.BadRequest(renamed.Error.Message);
			}

			break;
		}

		// category definitions
		case "saveCategory":
		{
			var categories = store.LoadCategories(CategorySeed.Keywords);
			var saved = SaveCategory(categories, r);
			if (saved.IsFailure)
			{
				return Results.BadRequest(saved.Error.Message);
			}

			store.SaveCategories(categories);
			result = new { Id = saved.Value.Value };
			break;
		}

		case "deleteCategory" when Has(r.Id, out var id):
		{
			var categories = store.LoadCategories(CategorySeed.Keywords);
			var category = categories.FirstOrDefault(x => string.Equals(x.Id.Value, id, StringComparison.Ordinal));
			if (category is null)
			{
				return Results.NotFound("This category does not exist.");
			}

			if (category.Id == CategoryId.Other)
			{
				return Results.BadRequest(CategoryErrors.OtherCannotBeDeleted.Message);
			}

			categories.Remove(category);
			store.SaveCategories(categories);

			// Its payees go back to automatic categorisation (and show up in "To review").
			user.ForgetCategory(category.Id);
			break;
		}

		default:
			return Results.BadRequest("Unknown action, or a field it needs is missing.");
	}

	store.SaveUserData(user);
	return Results.Ok(result);
});

// Reads a request field an action needs: true, with the value, when it is present and not blank.
static bool Has(string? field, out string value)
{
	value = field ?? "";
	return !string.IsNullOrWhiteSpace(field);
}

// Creates a category (its id is the slug of its first name, made unique) or updates an existing one.
Result<CategoryId> SaveCategory(IList<Category> categories, ApiRequest r)
{
	var name = r.Name ?? "";
	var type = Enum.TryParse<CategoryType>(r.Type, out var parsed) ? parsed : CategoryType.Expense;
	var keywords = (r.Keywords ?? "").Split(keywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	var existing = categories.FirstOrDefault(x => string.Equals(x.Id.Value, r.Id, StringComparison.Ordinal));
	if (existing is not null)
	{
		var changed = existing.Update(name, type, r.Color ?? existing.Color, keywords);
		return changed.IsSuccess ? existing.Id : Result.Failure<CategoryId>(changed.Error);
	}

	var slug = Store.Slug(name);
	if (slug.Length == 0)
	{
		return Result.Failure<CategoryId>(CategoryErrors.NameRequired);
	}

	var id = slug;
	var suffix = 2;
	while (categories.Any(x => string.Equals(x.Id.Value, id, StringComparison.Ordinal)))
	{
		id = string.Create(CultureInfo.InvariantCulture, $"{slug}-{suffix++}");
	}

	var created = Category.Create(new CategoryId(id), name, type, r.Color ?? Category.DefaultColor, keywords, builtIn: false);
	if (created.IsFailure)
	{
		return Result.Failure<CategoryId>(created.Error);
	}

	categories.Add(created.Value);
	return created.Value.Id;
}

// ---------- bank connection ----------
app.MapGet("/banks", async (EnableBankingClient eb) => HtmlResult(BankPickerPage.Render([.. await eb.GetBanksAsync()])));

app.MapGet("/connect", async (string name, string country, EnableBankingClient eb) =>
{
	var state = Guid.NewGuid().ToString();
	pendingStates.Add(state);
	return Results.Redirect(await eb.StartAuthAsync(name, country, state));
});

app.MapGet("/callback", async (string? code, string? state, string? error, string? error_description, EnableBankingClient eb, Store store, TimeProvider clock) =>
{
	if (error is not null) return Results.Content($"Bank authorisation failed: {error} {error_description}", "text/plain");
	if (code is null || state is null || !pendingStates.Remove(state)) return Results.BadRequest("Unknown or expired authorisation request — start again from /banks.");

	var (sessionId, uids, _) = await eb.CreateSessionAsync(code);
	var old = store.LoadSession();
	var now = clock.GetUtcNow();
	var session = SessionInfo.Connect(new BankSessionId(sessionId), uids, now, now.AddDays(ebOptions.ConsentDays), old?.LastSync);
	if (session.IsFailure)
	{
		return Results.Content($"Bank authorisation failed: {session.Error.Message}", "text/plain");
	}

	store.SaveSession(session.Value);
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
