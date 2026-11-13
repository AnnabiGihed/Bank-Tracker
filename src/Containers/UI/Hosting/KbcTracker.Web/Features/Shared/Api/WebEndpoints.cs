using System.Text;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Shared.Features.Backend;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The two routes that are not components: the bank's callback navigation and the CSV download. Every
///              other action goes from the components straight to the central backend.
/// </summary>
public static class WebEndpoints
{
	#region Public Methods
	/// <summary>Maps the callback and the export without registering a local store, bank client or sync worker.</summary>
	/// <param name="app">The web host.</param>
	public static void Map(WebApplication app)
	{
		ArgumentNullException.ThrowIfNull(app);
		app.MapGet("/callback", CompleteConsentAsync);
		app.MapGet("/export.csv", async (BackendClient backend, CancellationToken ct) =>
			Results.File(Encoding.UTF8.GetBytes(CsvExport.Write((await backend.GetWorkspaceAsync(ct).ConfigureAwait(false)).ToAnalysis())), "text/csv", "kbc-transactions.csv"));
	}
	#endregion

	#region Private Helpers
	/// <summary>Forwards a callback for state validation and returns to Settings, with a notice when the connection
	/// failed.</summary>
	private static async Task<IResult> CompleteConsentAsync(string? code, string? state, string? error, HttpContext context, BackendClient backend,
		CancellationToken ct)
	{
		var reply = await backend.CompleteConsentAsync(code, state, error, ct).ConfigureAwait(false);
		if (reply.IsSuccess)
		{
			return Results.Redirect("/settings");
		}

		PageNotice.Set(context.Response, $"Bank connection failed: {reply.Error}");
		return Results.LocalRedirect("/settings");
	}
	#endregion
}
