using KbcTracker.ApiService.Features.Authentication;
using KbcTracker.ApiService.Features.Shared;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.BankConnection.Queries;
using Microsoft.AspNetCore.Mvc;
using Pivot.Framework.Application.Abstractions.Messaging;
using Pivot.Framework.Containers.API.Abstractions;

namespace KbcTracker.ApiService.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Exposes bank consent through the same protected central API used by both clients.
/// </summary>
/// <param name="sender">The application mediator.</param>
[Route(ApiRoutes.Prefix)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BankConsentController(ISender sender) : ApiController(sender)
{
	#region Endpoints
	/// <summary>Lists the available bank identities.</summary>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The safe bank catalogue.</returns>
	[HttpGet(ApiRoutes.Banks, Name = nameof(ListBanks))]
	[ProducesResponseType<IReadOnlyList<BankDefinition>>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
	public async Task<ActionResult> ListBanks(CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new ListBanksQuery(), cancellationToken).ConfigureAwait(false));

	/// <summary>Starts an owner-bound read-only bank consent request.</summary>
	/// <param name="request">The bank selected from the catalogue.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The HTTPS bank authorisation URL, or a safe problem.</returns>
	[HttpPost(ApiRoutes.Consent, Name = nameof(BeginConsent))]
	[Consumes("application/json")]
	[ProducesResponseType<ConsentStarted>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
	public async Task<ActionResult> BeginConsent(BeginConsentRequest request, CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new BeginBankConsentCommand(request, Owner()), cancellationToken).ConfigureAwait(false));

	/// <summary>Consumes a consent redirect and persists the resulting connection without disclosing credentials.</summary>
	/// <param name="code">The one-use bank code, if consent succeeded.</param>
	/// <param name="state">The owner-bound request state.</param>
	/// <param name="error">A provider failure marker, never echoed in the response.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>The safe connection status, or an invalid-state/provider/conflict problem.</returns>
	[HttpGet(ApiRoutes.ConsentCallback, Name = nameof(CompleteConsent))]
	[ProducesResponseType<ConnectionStatus>(StatusCodes.Status200OK, "application/json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
	public async Task<ActionResult> CompleteConsent(string? code, string? state, string? error, CancellationToken cancellationToken) =>
		HandleResult(await Sender.Send(new CompleteBankConsentCommand(code, state, error, Owner()), cancellationToken).ConfigureAwait(false));
	#endregion

	#region Private Helpers
	/// <summary>Binds the consent to the owner id the token carries; the fallback policy has already required it.</summary>
	private string Owner() => User.FindFirst(OwnerIdentity.ClaimType)?.Value
		?? throw new InvalidOperationException("An authorised request carries the owner claim.");
	#endregion
}
