using Pivot.Framework.Application.Abstractions.Messaging.Commands;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Begins the central consent workflow through Pivot's mediator.
/// </summary>
/// <param name="workflow">The central consent workflow.</param>
internal sealed class BeginBankConsentCommandHandler(BankConsentWorkflow workflow) : ICommandHandler<BeginBankConsentCommand, ConsentStarted>
{
	#region ICommandHandler Implementation
	/// <inheritdoc />
	public Task<Result<ConsentStarted>> Handle(BeginBankConsentCommand request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		return workflow.BeginAsync(request.Selection, request.Owner, cancellationToken);
	}
	#endregion
}
