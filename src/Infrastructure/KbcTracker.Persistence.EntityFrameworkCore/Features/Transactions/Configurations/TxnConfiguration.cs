namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Transactions.Configurations;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps transactions to the <c>Transactions</c> table. Amounts are unbounded <c>numeric</c> so
///              imported values keep their exact scale, and the bank's original JSON stays <c>text</c>: <c>jsonb</c>
///              would reorder its keys, and the analysis reads it as sent.
/// </summary>
internal sealed class TxnConfiguration : IEntityTypeConfiguration<Txn>
{
	#region IEntityTypeConfiguration Implementation
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Txn> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.ToTable("Transactions");
		builder.HasKey(t => t.Id);
		builder.Property(t => t.Id).ValueGeneratedNever();
		builder.Property(t => t.AccountUid).IsRequired();
		builder.Property(t => t.Date);
		builder.Property(t => t.Amount).HasColumnType("numeric");
		builder.Property(t => t.Currency).IsRequired();
		builder.Property(t => t.Counterparty).IsRequired();
		builder.Property(t => t.Description).IsRequired();
		builder.Property(t => t.CounterpartyIban);
		builder.Property(t => t.BankCode);
		builder.Property(t => t.Raw).HasColumnType("text");
		builder.HasIndex(t => new { t.AccountUid, t.Date });
	}
	#endregion
}
