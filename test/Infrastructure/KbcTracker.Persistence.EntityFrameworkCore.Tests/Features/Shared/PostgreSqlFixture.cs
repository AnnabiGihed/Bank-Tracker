using Npgsql;
using Testcontainers.PostgreSql;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Starts one PostgreSQL container for the whole test run and gives each test its own freshly
///              migrated database, so tests never share data and the migration itself is exercised.
///
///              Docker must be running. Without it, starting the container fails and every test fails with that
///              cause; nothing is reported as passed. The tests never connect to the user's own database.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
	#region Constants
	/// <summary>
	/// The pinned PostgreSQL image, the same major version the home PC runs (ADR-0004).
	/// </summary>
	private const string Image = "postgres:17.6-alpine";
	#endregion

	#region Fields
	/// <summary>
	/// The container shared by every test.
	/// </summary>
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public Task InitializeAsync() => _container.StartAsync();

	/// <inheritdoc />
	public Task DisposeAsync() => _container.DisposeAsync().AsTask();
	#endregion

	#region Public Methods
	/// <summary>
	/// Creates a new, migrated database and the services that use it, as a host registers them.
	/// </summary>
	/// <returns>The services; dispose them at the end of the test.</returns>
	public async Task<ServiceProvider> CreateDatabaseAsync()
	{
		var connection = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
		{
			Database = "kbc_" + Guid.NewGuid().ToString("N"),
		};

		// Logging comes from the host in production; Pivot's domain event publisher needs it.
		var services = new ServiceCollection().AddLogging().AddKbcTrackerPersistence(connection.ConnectionString).BuildServiceProvider();
		var scope = services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			await scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>().Database.MigrateAsync().ConfigureAwait(false);
		}

		return services;
	}
	#endregion
}
