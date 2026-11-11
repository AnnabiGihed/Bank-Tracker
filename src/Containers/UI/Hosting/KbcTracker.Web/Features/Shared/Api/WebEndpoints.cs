using System.Diagnostics.CodeAnalysis;
using System.Text;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Shared.Features.Backend;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Renders existing pages from the API and forwards every mutation to the central backend.
/// </summary>
public static class WebEndpoints
{
	#region Public Methods
	/// <summary>Maps the UI routes without registering a local store, bank client or sync worker.</summary>
	/// <param name="app">The web host.</param>
	public static void Map(WebApplication app)
	{
		app.MapGet("/", () => Results.Redirect("/overview"));
		MapPages(app);
		app.MapPost("/api", DecideAsync);
		app.MapGet("/banks", async (HttpContext context, BackendClient backend, CancellationToken ct) =>
			Html(BankPickerPage.Render((await backend.GetBanksAsync(ct).ConfigureAwait(false)).Select(b => (b.Name, b.Country)).ToList(), PageNotice.Take(context))));
		app.MapPost("/connect", BeginConsentAsync);
		app.MapGet("/connect", () => Results.Redirect("/banks"));
		app.MapGet("/callback", CompleteConsentAsync);
		app.MapPost("/sync", SyncAsync);
		app.MapGet("/sync", () => Results.Redirect("/settings"));
		app.MapGet("/export.csv", async (BackendClient backend, CancellationToken ct) =>
			Results.File(Encoding.UTF8.GetBytes(CsvExport.Write((await backend.GetWorkspaceAsync(ct).ConfigureAwait(false)).ToAnalysis())), "text/csv", "kbc-transactions.csv"));
	}
	#endregion

	#region Private Helpers
	/// <summary>Maps each tab to the central workspace while retaining its query filters.</summary>
	private static void MapPages(WebApplication app)
	{
		app.MapGet("/overview", (string? m, HttpContext context, BackendClient backend, CancellationToken ct) => PageAsync(OverviewPage.Render, m, context, backend, ct));
		app.MapGet("/fixed", (string? m, HttpContext context, BackendClient backend, CancellationToken ct) => PageAsync(FixedPaymentsPage.Render, m, context, backend, ct));
		app.MapGet("/review", (string? m, HttpContext context, BackendClient backend, CancellationToken ct) => PageAsync(ReviewPage.Render, m, context, backend, ct));
		app.MapGet("/categories", (string? m, HttpContext context, BackendClient backend, CancellationToken ct) => PageAsync(CategoriesPage.Render, m, context, backend, ct));
		app.MapGet("/settings", (string? m, HttpContext context, BackendClient backend, CancellationToken ct) => PageAsync(SettingsPage.Render, m, context, backend, ct));
		app.MapGet("/transactions", async (string? m, string? cat, string? q, HttpContext context, BackendClient backend, CancellationToken ct) =>
		{
			var workspace = await backend.GetWorkspaceAsync(ct).ConfigureAwait(false);
			var analysis = workspace.ToAnalysis();
			return Html(TransactionsPage.Render(analysis, Month(m, analysis), workspace.ToDisplaySession(), cat, q, PageNotice.Take(context)));
		});
	}

	/// <summary>Renders one tab from a single backend response, with the notice of an action that just failed.</summary>
	private static async Task<IResult> PageAsync(Func<Analysis, string, SessionInfo?, string?, string> render, string? month, HttpContext context,
		BackendClient backend, CancellationToken ct)
	{
		var workspace = await backend.GetWorkspaceAsync(ct).ConfigureAwait(false);
		var analysis = workspace.ToAnalysis();
		return Html(render(analysis, Month(month, analysis), workspace.ToDisplaySession(), PageNotice.Take(context)));
	}

	/// <summary>Preserves the browser's existing action response shapes while the API owns all writes.</summary>
	private static async Task<IResult> DecideAsync(DecisionRequest request, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.DecideAsync(request, ct).ConfigureAwait(false);
		if (!reply.IsSuccess)
		{
			return Failure(reply);
		}

		return request.Action switch
		{
			"saveCategory" => Results.Ok(new { reply.Value?.Id }),
			"payeeCategory" => Results.Json(reply.Value?.PreviousAssignment),
			_ => Results.Json<object?>(null),
		};
	}

	/// <summary>Starts consent on the backend and redirects only to the provider address it returns; a refusal returns to
	/// the bank picker with a notice.</summary>
	private static async Task<IResult> BeginConsentAsync(string name, string country, HttpContext context, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.BeginConsentAsync(new BeginConsentRequest(name, country), ct).ConfigureAwait(false);
		return reply.IsSuccess && reply.Value is not null
			? Results.Redirect(reply.Value.AuthorizationUrl.ToString())
			: ReturnWithNotice(context, "/banks", "Could not connect to the bank", reply);
	}

	/// <summary>Forwards a callback for state validation and returns to Settings, with a notice when the connection
	/// failed.</summary>
	private static async Task<IResult> CompleteConsentAsync(string? code, string? state, string? error, HttpContext context, BackendClient backend,
		CancellationToken ct)
	{
		var reply = await backend.CompleteConsentAsync(code, state, error, ct).ConfigureAwait(false);
		return reply.IsSuccess ? Results.Redirect("/settings") : ReturnWithNotice(context, "/settings", "Bank connection failed", reply);
	}

	/// <summary>Runs manual sync through the API and returns only to a local page, with a notice when the sync failed.</summary>
	private static async Task<IResult> SyncAsync(bool? full, string? back, HttpContext context, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.SyncAsync(full is true, ct).ConfigureAwait(false);
		var page = IsLocalPath(back) ? back : "/settings";
		return reply.IsSuccess ? Results.LocalRedirect(page) : ReturnWithNotice(context, page, "Sync failed", reply);
	}

	/// <summary>Returns a browser form to a local page with the backend's safe message, instead of a problem document the
	/// browser would show raw.</summary>
	private static IResult ReturnWithNotice<T>(HttpContext context, string page, string action, BackendReply<T> reply)
	{
		PageNotice.Set(context.Response, $"{action}: {reply.Error}");
		return Results.LocalRedirect(page);
	}

	/// <summary>Accepts only a path on this site: browsers read <c>//host</c> and <c>/\host</c> as another site and drop
	/// tabs and line breaks before deciding, so those are refused too.</summary>
	private static bool IsLocalPath([NotNullWhen(true)] string? path) => path is ['/', ..]
		&& !path.StartsWith("//", StringComparison.Ordinal)
		&& !path.Contains('\\', StringComparison.Ordinal)
		&& !path.Any(char.IsControl);

	/// <summary>Returns safe API validation details using its original status code.</summary>
	private static IResult Failure<T>(BackendReply<T> reply) => Results.Problem(statusCode: reply.StatusCode, title: "Request could not complete.", detail: reply.Error);

	/// <summary>Selects a known month or the newest available one.</summary>
	private static string Month(string? requested, Analysis analysis) => requested is not null && analysis.AvailableMonths.Contains(requested)
		? requested : analysis.AvailableMonths.FirstOrDefault() ?? string.Empty;

	/// <summary>Returns a rendered page as HTML.</summary>
	private static IResult Html(string content) => Results.Content(content, "text/html");
	#endregion
}
