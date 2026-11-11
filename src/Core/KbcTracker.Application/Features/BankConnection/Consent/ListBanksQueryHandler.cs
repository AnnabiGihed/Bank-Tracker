using Pivot.Framework.Application.Abstractions.Messaging.Queries;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Routes bank catalogue queries to the credential-owning backend workflow.
/// </summary>
/// <param name="workflow">The central bank consent workflow.</param>
internal sealed class ListBanksQueryHandler(BankConsentWorkflow workflow) : IQueryHandler<ListBanksQuery, IReadOnlyList<BankDefinition>>
{
	#region IQueryHandler Implementation
	/// <inheritdoc />
	public Task<Result<IReadOnlyList<BankDefinition>>> Handle(ListBanksQuery request, CancellationToken cancellationToken) =>
		workflow.GetBanksAsync(cancellationToken);
	#endregion
}
