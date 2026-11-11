namespace KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection.Configurations;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps bank connections to the <c>BankSessions</c> table, one row per consent. The row version
///              (PostgreSQL's <c>xmin</c>) makes a save fail when another process changed the connection meanwhile,
///              instead of overwriting it.
/// </summary>
internal sealed class SessionInfoConfiguration : IEntityTypeConfiguration<SessionInfo>
{
	#region IEntityTypeConfiguration Implementation
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<SessionInfo> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.ToTable("BankSessions");
		builder.HasKey(s => s.Id);
		builder.Property(s => s.Id).ValueGeneratedNever();
		builder.Ignore(s => s.AccountUids);
		builder.PrimitiveCollection<List<string>>("_accountUids").HasColumnName("AccountUids").IsRequired();
		builder.Property(s => s.ConnectedAt);
		builder.Property(s => s.ValidUntil);
		builder.Property(s => s.LastSync);
		builder.Property(s => s.LastError).HasMaxLength(SessionInfo.MaxErrorLength);
		builder.Property(s => s.SkippedTransactions);
		builder.Property<uint>("RowVersion").IsRowVersion();
	}
	#endregion
}
