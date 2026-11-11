using KbcTracker.Application.Features.Transactions.Queries;
using KbcTracker.Application.Features.Categories.Queries;
using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.BankConnection.Commands;
using Microsoft.AspNetCore.Mvc;
using Pivot.Framework.Application.Abstractions.Messaging;
using Pivot.Framework.Containers.API.Abstractions;

namespace KbcTracker.ApiService.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Dispatches the first versioned backend operations through Pivot application handlers.
/// </summary>
/// <param name="sender">The shared application mediator.</param>
[Route(ApiRoutes.Prefix)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class BackendController(ISender sender) : ApiController(sender)
{
	#region Endpoints
	/// <summary>Reads a bounded page of permanent transaction history.</summary>
	/// <param name="skip">The row offset, not negative.</param>
	/// <param name="take">The page size, between 1 and 500.</param>
	/// <param name="cancellationToken">Cancels the query.</param>
	/// <returns>The page, or a validation problem.</returns>
	[HttpGet(ApiRoutes.Transactions, Name = nameof(ListTransactions))]
	[ProducesResponseType<IReadOnlyList<TransactionSummary>>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	public async Task<ActionResult> ListTransactions(int skip = 0, int take = 100, CancellationToken cancellationToken = default) =>
		HandleResult(await Sender.Send(new ListTransactionsQuery(skip, take), cancellationToken).ConfigureAwait(false));

	/// <summary>Reads the central category definitions.</summary>
	/// <param name="cancellationToken">Cancels the query.</param>
	/// <returns>The definitions, without creating defaults.</returns>
	[HttpGet(ApiRoutes.Categories, Name = nameof(ListCategories))]
	[ProducesResponseType<IReadOnlyList<CategorySummary>>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	public async Task<ActionResult> ListCategories(CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new ListCategoriesQuery(), cancellationToken).ConfigureAwait(false));

	/// <summary>Reads consent and sync status without exposing bank credentials or raw errors.</summary>
	/// <param name="cancellationToken">Cancels the query.</param>
	/// <returns>The client-safe status.</returns>
	[HttpGet(ApiRoutes.Connection, Name = nameof(GetConnection))]
	[ProducesResponseType<ConnectionStatus>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	public async Task<ActionResult> GetConnection(CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new GetConnectionStatusQuery(), cancellationToken).ConfigureAwait(false));

	/// <summary>Runs a bank sync, requesting full history when selected or when history is empty.</summary>
	/// <param name="full">Whether to request the longest available history.</param>
	/// <param name="cancellationToken">Cancels the workflow.</param>
	/// <returns>The durably added count, or a safe conflict problem when unavailable.</returns>
	[HttpPost(ApiRoutes.Sync, Name = nameof(SyncBank))]
	[ProducesResponseType<SyncSummary>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	public async Task<ActionResult> SyncBank(bool full = false, CancellationToken cancellationToken = default) =>
		HandleResult(await Sender.Send(new SyncBankCommand(full), cancellationToken).ConfigureAwait(false));

	/// <summary>Reports process health without querying PostgreSQL or revealing configuration.</summary>
	/// <returns>A constant health response.</returns>
	[HttpGet(ApiRoutes.HealthTemplate, Name = nameof(GetHealth))]
	[ProducesResponseType<string>(StatusCodes.Status200OK, "text/plain")]
	public ActionResult GetHealth() => Ok("healthy");
	#endregion
}
