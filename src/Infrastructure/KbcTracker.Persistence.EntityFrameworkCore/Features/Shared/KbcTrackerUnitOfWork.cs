using Pivot.Framework.Application.Abstractions;
using Pivot.Framework.Infrastructure.Abstraction.Outbox.DomainEventPublisher;
using Pivot.Framework.Infrastructure.Abstraction.Outbox.IntegrationEventMapping;
using Pivot.Framework.Infrastructure.Persistence.EntityFrameworkCore.UnitOfWork;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The unit of work of KbcTracker: commits every change of one use case in a single transaction
///              through Pivot's <see cref="UnitOfWork{TContext}"/>. It adds nothing to Pivot's behaviour.
/// </summary>
/// <param name="dbContext">The context whose changes are saved.</param>
/// <param name="currentUserProvider">Who made the change, for audit stamping.</param>
/// <param name="domainEventPublisher">Writes domain events to the outbox (none are raised yet).</param>
/// <param name="integrationEventMappingCoordinator">Maps domain events to integration events, when registered;
/// forwarded so a later registration is not silently ignored.</param>
public sealed class KbcTrackerUnitOfWork(
	KbcTrackerDbContext dbContext,
	ICurrentUserProvider currentUserProvider,
	IDomainEventPublisher<KbcTrackerDbContext> domainEventPublisher,
	IIntegrationEventMappingCoordinator<KbcTrackerDbContext>? integrationEventMappingCoordinator = null)
	: UnitOfWork<KbcTrackerDbContext>(dbContext, currentUserProvider, domainEventPublisher, integrationEventMappingCoordinator);
