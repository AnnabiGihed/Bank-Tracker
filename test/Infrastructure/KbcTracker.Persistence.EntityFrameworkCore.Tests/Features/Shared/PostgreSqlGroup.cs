namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Shares one <see cref="PostgreSqlFixture"/> container across the test classes that use it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgreSqlGroup : ICollectionFixture<PostgreSqlFixture>
{
	#region Constants
	/// <summary>
	/// The collection name the test classes refer to.
	/// </summary>
	public const string Name = "PostgreSQL";
	#endregion
}
