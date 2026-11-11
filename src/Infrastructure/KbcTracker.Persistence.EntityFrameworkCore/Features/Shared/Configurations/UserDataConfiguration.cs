namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Configurations;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps the user's decisions to the single-row <c>UserData</c> table, with the accepted fixed payments
///              it owns in <c>RecurringPayments</c>. Payee categories, transaction categories, payee names and
///              dismissed suggestions are <c>jsonb</c> columns read and written through the aggregate's private
///              fields. The row version makes a concurrent save fail instead of losing a decision.
/// </summary>
internal sealed class UserDataConfiguration : IEntityTypeConfiguration<UserData>
{
	#region IEntityTypeConfiguration Implementation
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<UserData> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.ToTable("UserData");
		builder.HasKey(u => u.Id);
		builder.Property(u => u.Id).ValueGeneratedNever();
		builder.Property<uint>("RowVersion").IsRowVersion();

		builder.Ignore(u => u.Recurring);
		builder.Ignore(u => u.DismissedSuggestions);
		builder.Ignore(u => u.Payees);
		builder.Ignore(u => u.TxnCategories);
		builder.Ignore(u => u.PayeeNames);

		builder.Property<HashSet<string>>("_dismissedSuggestions").HasColumnName("DismissedSuggestions").HasColumnType("jsonb")
			.HasConversion(JsonColumns.SignaturesConverter, JsonColumns.SignaturesComparer);
		builder.Property<Dictionary<string, PayeeAssignment>>("_payees").HasColumnName("Payees").HasColumnType("jsonb")
			.HasConversion(JsonColumns.PayeesConverter, JsonColumns.PayeesComparer);
		builder.Property<Dictionary<TxnId, CategoryId>>("_txnCategories").HasColumnName("TxnCategories").HasColumnType("jsonb")
			.HasConversion(JsonColumns.TxnCategoriesConverter, JsonColumns.TxnCategoriesComparer);
		builder.Property<Dictionary<string, string>>("_payeeNames").HasColumnName("PayeeNames").HasColumnType("jsonb")
			.HasConversion(JsonColumns.NamesConverter, JsonColumns.NamesComparer);

		builder.OwnsMany<RecurringPayment>("_recurring", ConfigureRecurringPayment);
		builder.Navigation("_recurring").UsePropertyAccessMode(PropertyAccessMode.Field);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Maps the accepted fixed payments, owned by the user's decisions.
	/// </summary>
	private static void ConfigureRecurringPayment(OwnedNavigationBuilder<UserData, RecurringPayment> payment)
	{
		payment.ToTable("RecurringPayments");
		payment.WithOwner().HasForeignKey("UserDataId");
		payment.HasKey(p => p.Id);
		payment.Property(p => p.Id).ValueGeneratedNever();
		payment.Property(p => p.Name).IsRequired();
		payment.Property(p => p.CategoryId);
		payment.Property(p => p.Amount).HasColumnType("numeric");
		payment.Property(p => p.TolerancePct).HasColumnType("numeric");
		payment.Property(p => p.CadenceDays);
		payment.Property(p => p.Active);
		payment.Property(p => p.CreatedAt);
		payment.Ignore(p => p.SeriesKeys);
		payment.Ignore(p => p.Tokens);
		payment.Ignore(p => p.Signature);
		payment.PrimitiveCollection<List<string>>("_seriesKeys").HasColumnName("SeriesKeys").IsRequired();
		payment.PrimitiveCollection<List<string>>("_tokens").HasColumnName("Tokens").IsRequired();
	}
	#endregion
}
