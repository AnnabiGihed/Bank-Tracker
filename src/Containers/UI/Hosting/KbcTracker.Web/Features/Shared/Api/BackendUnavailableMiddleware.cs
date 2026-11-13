using KbcTracker.ViewModels.Features.Shared;

namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Turns a backend failure on the routes that call it directly (the CSV export, the bank callback) into a
///              safe problem response, without exposing transport details: a refusal (403) or conflict (409) says what the
///              backend refused, anything else that the backend is unavailable. The components show the same wording
///              themselves.
/// </summary>
/// <param name="next">The pipeline that calls the API.</param>
public sealed class BackendUnavailableMiddleware(RequestDelegate next)
{
	#region Public Methods
	/// <summary>Converts backend transport, status and contract failures into a problem response with a matching status.</summary>
	/// <param name="context">The current web request.</param>
	/// <returns>A task that completes after the response.</returns>
	/// <exception cref="ArgumentNullException">The request is null.</exception>
	public async Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		try
		{
			await next(context).ConfigureAwait(false);
		}
		catch (Exception exception) when (!context.Response.HasStarted && BackendProblem.Handles(exception, context.RequestAborted))
		{
			var problem = BackendProblem.FromException(exception);
			await Results.Problem(statusCode: problem.Status, title: problem.Title, detail: problem.Detail).ExecuteAsync(context).ConfigureAwait(false);
		}
	}
	#endregion
}
