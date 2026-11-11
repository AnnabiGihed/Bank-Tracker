using Pivot.Framework.Infrastructure.Persistence.EntityFrameworkCore.PersistenceContext;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The EF Core context of KbcTracker on PostgreSQL (ADR-0004): transactions, categories, bank
///              connections and the user's decisions.
///
///              Built on Pivot's <see cref="PivotDbContextBase"/>. Pivot adds no converters for strongly typed ids,
///              so the conventions here map every id to its string value and store every date and time in UTC, which
///              Npgsql requires for <c>timestamptz</c>. No outbox or event-history table is mapped: no aggregate
///              raises domain events yet.
/// </summary>
/// <param name="options">The options configured for this context.</param>
public sealed class KbcTrackerDbContext(DbContextOptions<KbcTrackerDbContext> options) : PivotDbContextBase(options)
{
	#region Properties
	/// <summary>
	/// Gets the transaction history of every account.
	/// </summary>
	public DbSet<Txn> Transactions => Set<Txn>();

	/// <summary>
	/// Gets the categories.
	/// </summary>
	public DbSet<Category> Categories => Set<Category>();

	/// <summary>
	/// Gets the bank connections, one per consent.
	/// </summary>
	public DbSet<SessionInfo> BankSessions => Set<SessionInfo>();

	/// <summary>
	/// Gets the user's decisions (a single row).
	/// </summary>
	public DbSet<UserData> UserData => Set<UserData>();
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		ArgumentNullException.ThrowIfNull(configurationBuilder);
		base.ConfigureConventions(configurationBuilder);
		configurationBuilder.Properties<TxnId>().HaveConversion<TxnIdConverter>();
		configurationBuilder.Properties<CategoryId>().HaveConversion<CategoryIdConverter>();
		configurationBuilder.Properties<BankSessionId>().HaveConversion<BankSessionIdConverter>();
		configurationBuilder.Properties<UserDataId>().HaveConversion<UserDataIdConverter>();
		configurationBuilder.Properties<RecurringPaymentId>().HaveConversion<RecurringPaymentIdConverter>();
		configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
	}

	/// <inheritdoc />
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);
		base.OnModelCreating(modelBuilder);
		modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
	}
	#endregion
}
