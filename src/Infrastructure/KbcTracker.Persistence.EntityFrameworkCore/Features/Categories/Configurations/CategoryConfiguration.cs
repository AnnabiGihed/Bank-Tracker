namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Categories.Configurations;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps categories to the <c>Categories</c> table. Keywords are a <c>text[]</c> kept with their exact
///              text and order: the trailing space of "AD " is what stops it matching "ADIDAS". The row version
///              (PostgreSQL's <c>xmin</c>) makes a save fail when another process changed the category meanwhile,
///              instead of overwriting it.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
	#region IEntityTypeConfiguration Implementation
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Category> builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.ToTable("Categories");
		builder.HasKey(c => c.Id);
		builder.Property(c => c.Id).ValueGeneratedNever();
		builder.Property(c => c.Name).IsRequired();
		builder.Property(c => c.Type).HasConversion<string>();
		builder.Property(c => c.Color).IsRequired();
		builder.Property(c => c.BuiltIn);
		builder.Ignore(c => c.Keywords);
		builder.PrimitiveCollection<List<string>>("_keywords").HasColumnName("Keywords").IsRequired();
		builder.Property<uint>("RowVersion").IsRowVersion();
	}
	#endregion
}
