using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Domain.Features.Transactions.Repositories;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using KbcTracker.Domain.Features.Categories.Repositories;
using KbcTracker.Domain.Features.Shared.Repositories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Transactions.Repositories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection.Repositories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Categories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Categories.Repositories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Repositories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;
using Microsoft.Extensions.DependencyInjection;
using Pivot.Framework.Domain.Repositories;
using Pivot.Framework.Infrastructure.Abstraction.UnitOfWork;
using Pivot.Framework.Infrastructure.Messaging.EntityFrameworkCore.Extensions;
using Pivot.Framework.Infrastructure.Persistence.PostgreSQL.Extensions;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the PostgreSQL persistence of KbcTracker in a host: the context, Pivot's unit of work
///              the data import and the category tree upgrade.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
	#region Public Methods
	/// <summary>
	/// Registers the context on PostgreSQL, the unit of work (also as Pivot's <see cref="IUnitOfWork"/>, which use
	/// cases depend on), the <see cref="DataImporter"/> and the <see cref="CategoryTreeUpgrade"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The PostgreSQL connection string; required.</param>
	/// <returns>The same service collection.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="connectionString"/> is blank.</exception>
	public static IServiceCollection AddKbcTrackerPersistence(this IServiceCollection services, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		services.AddPostgreSqlContext<KbcTrackerDbContext>(connectionString);
		services.AddEfCoreWritePersistence<KbcTrackerDbContext, KbcTrackerUnitOfWork>();

		// Pivot registers only the context-specific unit of work; use cases depend on the general one.
		services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IUnitOfWork<KbcTrackerDbContext>>());
		services.AddScoped<DataImporter>();
		services.AddScoped<CategoryTreeUpgrade>();
		services.AddScoped<ITransactionRepository, TransactionRepository>();
		services.AddScoped<IBankSessionRepository, BankSessionRepository>();
		services.AddScoped<ICategoryRepository, CategoryRepository>();
		services.AddScoped<IUserDataRepository, UserDataRepository>();
		services.AddScoped<IBankSyncPersistence, PostgreSqlSyncPersistence>();
		services.AddScoped<IWorkspacePersistence, PostgreSqlWorkspacePersistence>();
		return services;
	}
	#endregion
}
