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
		app.MapGet("/banks", async (BackendClient backend, CancellationToken ct) =>
			Html(BankPickerPage.Render((await backend.GetBanksAsync(ct).ConfigureAwait(false)).Select(b => (b.Name, b.Country)).ToList())));
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
		app.MapGet("/overview", (string? m, BackendClient backend, CancellationToken ct) => PageAsync(OverviewPage.Render, m, backend, ct));
		app.MapGet("/fixed", (string? m, BackendClient backend, CancellationToken ct) => PageAsync(FixedPaymentsPage.Render, m, backend, ct));
		app.MapGet("/review", (string? m, BackendClient backend, CancellationToken ct) => PageAsync(ReviewPage.Render, m, backend, ct));
		app.MapGet("/categories", (string? m, BackendClient backend, CancellationToken ct) => PageAsync(CategoriesPage.Render, m, backend, ct));
		app.MapGet("/settings", (string? m, BackendClient backend, CancellationToken ct) => PageAsync(SettingsPage.Render, m, backend, ct));
		app.MapGet("/transactions", async (string? m, string? cat, string? q, BackendClient backend, CancellationToken ct) =>
		{
			var workspace = await backend.GetWorkspaceAsync(ct).ConfigureAwait(false);
			var analysis = workspace.ToAnalysis();
			return Html(TransactionsPage.Render(analysis, Month(m, analysis), workspace.ToDisplaySession(), cat, q));
		});
	}

	/// <summary>Renders one tab from a single backend response.</summary>
	private static async Task<IResult> PageAsync(Func<Analysis, string, SessionInfo?, string> render, string? month, BackendClient backend, CancellationToken ct)
	{
		var workspace = await backend.GetWorkspaceAsync(ct).ConfigureAwait(false);
		var analysis = workspace.ToAnalysis();
		return Html(render(analysis, Month(month, analysis), workspace.ToDisplaySession()));
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

	/// <summary>Starts consent on the backend and redirects only to the provider address it returns.</summary>
	private static async Task<IResult> BeginConsentAsync(string name, string country, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.BeginConsentAsync(new BeginConsentRequest(name, country), ct).ConfigureAwait(false);
		return reply.IsSuccess && reply.Value is not null ? Results.Redirect(reply.Value.AuthorizationUrl.ToString()) : Failure(reply);
	}

	/// <summary>Forwards a callback for state validation and redirects after durable consent persistence.</summary>
	private static async Task<IResult> CompleteConsentAsync(string? code, string? state, string? error, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.CompleteConsentAsync(code, state, error, ct).ConfigureAwait(false);
		return reply.IsSuccess ? Results.Redirect("/settings") : Failure(reply);
	}

	/// <summary>Runs manual sync through the API and preserves only local return paths.</summary>
	private static async Task<IResult> SyncAsync(bool? full, string? back, BackendClient backend, CancellationToken ct)
	{
		var reply = await backend.SyncAsync(full is true, ct).ConfigureAwait(false);
		if (!reply.IsSuccess)
		{
			return Failure(reply);
		}

		return Results.LocalRedirect(IsLocalPath(back) ? back : "/settings");
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
