using Npgsql;
using Testcontainers.PostgreSql;

namespace KbcTracker.E2E.Tests;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs one disposable PostgreSQL container while giving each two-host test its own database.
/// </summary>
public sealed class BackendFixture : IAsyncLifetime
{
	#region Fields
	/// <summary>The real PostgreSQL dependency shared by the test class.</summary>
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public Task InitializeAsync() => _container.StartAsync();

	/// <inheritdoc />
	public Task DisposeAsync() => _container.DisposeAsync().AsTask();
	#endregion

	#region Public Methods
	/// <summary>Allocates an isolated database name for migration by the API test host.</summary>
	/// <returns>A connection string pointing only at the disposable container.</returns>
	public string CreateConnectionString() => new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
	{
		Database = "kbc_web_" + Guid.NewGuid().ToString("N"),
	}.ConnectionString;
	#endregion
}
