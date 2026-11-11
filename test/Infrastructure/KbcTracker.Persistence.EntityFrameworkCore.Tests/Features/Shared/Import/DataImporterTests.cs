using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared.Import;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the one-time import of the JSON data folder: everything arrives in one save, and a second
///              run is refused rather than duplicating or overwriting data.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
public sealed class DataImporterTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Import Tests
	/// <summary>
	/// Verifies that an import into an empty database stores every part of the snapshot.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Import_EmptyDatabase_EverythingStored()
	{
		var result = await ImportAsync(Snapshot());

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		(await ReadAsync(db => db.Transactions.CountAsync())).Should().Be(2);
		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(1);
		(await ReadAsync(db => db.UserData.CountAsync())).Should().Be(1);
		(await ReadAsync(db => db.BankSessions.CountAsync())).Should().Be(1);
	}

	/// <summary>
	/// Verifies that a second import is refused as a conflict and changes nothing.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Import_Twice_SecondRefused()
	{
		await ImportAsync(Snapshot());

		var second = await ImportAsync(Snapshot() with { Transactions = [Samples.Txn("t3")] });

		second.IsFailure.Should().BeTrue();
		second.Error.Should().Be(ImportErrors.DatabaseNotEmpty);
		second.ResultExceptionType.Should().Be(ResultExceptionType.Conflict);
		(await ReadAsync(db => db.Transactions.CountAsync())).Should().Be(2);
	}

	/// <summary>
	/// Verifies that a data folder without a bank connection imports too.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Import_NoSession_Stored()
	{
		var result = await ImportAsync(Snapshot() with { Session = null });

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		(await ReadAsync(db => db.BankSessions.CountAsync())).Should().Be(0);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// A data folder with two transactions, one category, the decisions and a connection.
	/// </summary>
	private static DataSnapshot Snapshot() =>
		new([Samples.Txn("t1"), Samples.Txn("t2")], [Samples.Groceries()], Samples.Decisions(), Samples.Session());

	/// <summary>
	/// Runs the importer in its own scope.
	/// </summary>
	private async Task<Result> ImportAsync(DataSnapshot snapshot)
	{
		var scope = Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			return await scope.ServiceProvider.GetRequiredService<DataImporter>().ImportAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		}
	}
	#endregion
}
