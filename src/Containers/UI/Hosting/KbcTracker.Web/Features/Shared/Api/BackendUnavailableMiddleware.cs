using System.Net;
using System.Text.Json;
using KbcTracker.Web.Features.Shared.Pages;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Turns a backend failure while rendering a page into a readable page of its own, without falling back
///              to local history or exposing transport details: a refusal (403) or conflict (409) says what the backend
///              refused, anything else that the backend is unavailable.
/// </summary>
/// <param name="next">The page pipeline that calls the API.</param>
public sealed class BackendUnavailableMiddleware(RequestDelegate next)
{
	#region Public Methods
	/// <summary>Converts backend transport, status and contract failures into a safe HTML page with a matching status.</summary>
	/// <param name="context">The current web request.</param>
	/// <returns>A task that completes after the page or failure response.</returns>
	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await next(context).ConfigureAwait(false);
		}
		catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested
			&& exception is HttpRequestException or JsonException or TaskCanceledException)
		{
			var (status, title, detail) = Describe((exception as HttpRequestException)?.StatusCode);
			context.Response.StatusCode = status;
			await Results.Content(PageKit.MessagePage(title, detail), "text/html").ExecuteAsync(context).ConfigureAwait(false);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Chooses the page for the status the backend answered with, or for no answer at all.</summary>
	private static (int Status, string Title, string Detail) Describe(HttpStatusCode? backendStatus) => backendStatus switch
	{
		HttpStatusCode.Forbidden => (StatusCodes.Status403Forbidden, "Backend refused access.",
			"The backend did not accept this web app. Check that Backend:OwnerLogin matches the backend's owner, and that the web app runs on the same PC."),
		HttpStatusCode.Conflict => (StatusCodes.Status409Conflict, "Backend not ready.",
			"The backend refused this request in its current state. If its database is new, run it once with --import-json or --initialize."),
		_ => (StatusCodes.Status503ServiceUnavailable, "Backend unavailable.", "Start the PostgreSQL backend and check its connection settings."),
	};
	#endregion
}
