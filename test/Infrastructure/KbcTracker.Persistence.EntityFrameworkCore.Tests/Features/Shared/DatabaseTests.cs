using Pivot.Framework.Domain.Repositories;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Base of the tests that run against PostgreSQL: a fresh migrated database per test, and helpers that
///              save through Pivot's unit of work and read back through a new scope, as two requests would.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
[Collection(PostgreSqlGroup.Name)]
public abstract class DatabaseTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
	#region Fields
	/// <summary>
	/// The services over this test's database.
	/// </summary>
	private ServiceProvider? _services;
	#endregion

	#region Properties
	/// <summary>
	/// Gets the services over this test's database.
	/// </summary>
	protected ServiceProvider Services => _services ?? throw new InvalidOperationException("The database is not created yet.");
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync() => _services = await fixture.CreateDatabaseAsync().ConfigureAwait(false);

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_services is not null)
		{
			await _services.DisposeAsync().ConfigureAwait(false);
		}
	}
	#endregion

	#region Protected Methods
	/// <summary>
	/// Changes the database in its own scope and saves through the unit of work.
	/// </summary>
	/// <param name="change">The change, on the scope's context.</param>
	/// <returns>The unit of work's result.</returns>
	protected async Task<Result> SaveAsync(Func<KbcTrackerDbContext, Task> change)
	{
		var scope = Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			await change(scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>()).ConfigureAwait(false);
			return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Adds entities in their own scope and saves them, failing the test with the cause when the save fails.
	/// </summary>
	/// <param name="entities">The entities to add.</param>
	/// <returns>A task that completes when they are saved.</returns>
	protected async Task AddAsync(params object[] entities)
	{
		var result = await SaveAsync(db =>
		{
			db.AddRange(entities);
			return Task.CompletedTask;
		}).ConfigureAwait(false);
		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
	}

	/// <summary>
	/// Reads from the database in a new scope, so nothing comes from a change tracker that saw the writes.
	/// </summary>
	/// <typeparam name="T">The value read.</typeparam>
	/// <param name="read">The query.</param>
	/// <returns>The value.</returns>
	protected async Task<T> ReadAsync<T>(Func<KbcTrackerDbContext, Task<T>> read)
	{
		var scope = Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			return await read(scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>()).ConfigureAwait(false);
		}
	}
	#endregion
}
