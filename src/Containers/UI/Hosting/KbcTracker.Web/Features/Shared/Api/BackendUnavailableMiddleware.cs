using System.Text.Json;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reports an unavailable backend without falling back to local history or exposing transport details.
/// </summary>
/// <param name="next">The page pipeline that calls the API.</param>
public sealed class BackendUnavailableMiddleware(RequestDelegate next)
{
	#region Public Methods
	/// <summary>Converts backend transport and contract failures into a safe service-unavailable response.</summary>
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
			await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Backend unavailable.",
				detail: "Start the PostgreSQL backend and check its connection settings.").ExecuteAsync(context).ConfigureAwait(false);
		}
	}
	#endregion
}
