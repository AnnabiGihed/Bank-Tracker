using KbcTracker.ApiService.Features.Shared;
using KbcTracker.Application.Features.Workspace.Commands;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Application.Features.Workspace.Queries;
using Microsoft.AspNetCore.Mvc;
using Pivot.Framework.Application.Abstractions.Messaging;
using Pivot.Framework.Containers.API.Abstractions;

namespace KbcTracker.ApiService.Features.Workspace;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Serves the central UI analysis and commits user decisions through application handlers.
/// </summary>
/// <param name="sender">The shared application mediator.</param>
[Route(ApiRoutes.Prefix)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class WorkspaceController(ISender sender) : ApiController(sender)
{
	#region Endpoints
	/// <summary>Reads the backend's computed workspace without exposing bank source records or credentials.</summary>
	/// <param name="cancellationToken">Cancels the query.</param>
	/// <returns>The workspace snapshot.</returns>
	[HttpGet(ApiRoutes.Workspace, Name = nameof(GetWorkspace))]
	[ProducesResponseType<WorkspaceSnapshot>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	public async Task<ActionResult> GetWorkspace(CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new GetWorkspaceQuery(), cancellationToken).ConfigureAwait(false));

	/// <summary>Applies a tracker decision and commits all affected category and user rows atomically.</summary>
	/// <param name="request">The decision fields.</param>
	/// <param name="cancellationToken">Cancels the command.</param>
	/// <returns>The saved category id or undo assignment.</returns>
	[HttpPost(ApiRoutes.Decisions, Name = nameof(ApplyDecision))]
	[Consumes("application/json")]
	[ProducesResponseType<DecisionResponse>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
	public async Task<ActionResult> ApplyDecision([FromBody] DecisionRequest request, CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new ApplyDecisionCommand(request), cancellationToken).ConfigureAwait(false));
	#endregion
}
