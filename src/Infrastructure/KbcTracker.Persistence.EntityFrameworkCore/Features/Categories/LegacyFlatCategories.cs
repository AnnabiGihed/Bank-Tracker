namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A frozen copy of the flat category list that databases were seeded with before the category tree: the
///              built-in categories plus the old default seed, exactly as they were stored. <see cref="CategoryTreeUpgrade"/>
///              compares a stored category with it to tell a category the owner never touched from one the owner
///              edited. It must never change, even when the defaults in the domain do.
/// </summary>
internal static class LegacyFlatCategories
{
	#region Static Instances
	/// <summary>
	/// The flat categories by id, as they were seeded.
	/// </summary>
	public static readonly IReadOnlyDictionary<string, Entry> ById = new[]
	{
		new Entry("income", "Income", CategoryType.Income, "#27ae60", []),
		new Entry("refunds", "Refunds", CategoryType.Income, "#6fcf97", []),
		new Entry("savings", "Savings & investments", CategoryType.Savings, "#9b51e0", []),
		new Entry("own-transfers", "Own transfers", CategoryType.Transfer, "#bdbdbd", ["REVOLUT", "WISE", "N26"]),
		new Entry("loan", "Loan", CategoryType.Expense, "#c0392b", []),
		new Entry("bills", "Bills & direct debits", CategoryType.Expense, "#2d9cdb", []),
		new Entry("bank-fees", "Bank & fees", CategoryType.Expense, "#828282", ["KOSTEN", "FRAIS BANCAIRES"]),
		new Entry("cash", "Cash", CategoryType.Expense, "#f2c94c", ["GELDOPNAME", "RETRAIT", "CASH", "ATM", "BANCONTACT GELDAUTOMAAT"]),
		new Entry("transfers", "Transfers to others", CategoryType.Expense, "#56ccf2", []),
		new Entry("other", "Other", CategoryType.Expense, "#a0a7b4", []),
		new Entry("car", "Car", CategoryType.Expense, "#3454d1", ["AUTOMOBILE", "TOYOTA", "GARAGE", "CARWASH", "FITWASH", "D'IETEREN", "VROOM"]),
		new Entry("groceries", "Groceries", CategoryType.Expense, "#f2994a", ["DELHAIZE", "COLRUYT", "CARREFOUR", "ALDI", "LIDL", "AD ", "OKAY", "SPAR", "INTERMARCHE", "ALBERT HEIJN", "CORA", "MATCH", "PROXY"]),
		new Entry("restaurants-cafes", "Restaurants & cafes", CategoryType.Expense, "#27ae60", ["RESTAURANT", "CAFE", "MCDONALD", "QUICK", "BURGER", "PIZZA", "DELIVEROO", "UBER EATS", "UBER *EATS", "TAKEAWAY", "FEDOREST", "EXKI", "PAUL"]),
		new Entry("transport-fuel", "Transport & fuel", CategoryType.Expense, "#9b51e0", ["STIB", "MIVB", "SNCB", "NMBS", "DE LIJN", "TEC", "EUROSTAR", "SNCF", "OUIGO", "RATP", "FLIXBUS", "TOTAL", "Q8", "SHELL", "ESSO", "LUKOIL", "UBER", "BOLT", "PARKING", "INDIGO", "INTERPARKING"]),
		new Entry("shopping", "Shopping", CategoryType.Expense, "#eb5757", ["AMAZON", "AMZN", "BOL.COM", "ZALANDO", "COOLBLUE", "MEDIA MARKT", "MEDIAMARKT", "IKEA", "ACTION", "HEMA", "PRIMARK", "DECATHLON", "FNAC", "KRUIDVAT"]),
		new Entry("health", "Health", CategoryType.Expense, "#2d9cdb", ["PHARMACIE", "APOTHEEK", "MUTUALITE", "MUTUALITEIT", "PARTENA", "HELAN", "SOLIDARIS", "CM ", "DOKTER", "DOCTEUR"]),
		new Entry("utilities-telecom", "Utilities & telecom", CategoryType.Expense, "#f2c94c", ["ENGIE", "ELECTRABEL", "LUMINUS", "TOTALENERGIES", "VIVAQUA", "SIBELGA", "PROXIMUS", "ORANGE", "TELENET", "BASE", "VOO", "SCARLET", "MOBILE VIKINGS"]),
		new Entry("subscriptions", "Subscriptions", CategoryType.Expense, "#6fcf97", ["NETFLIX", "SPOTIFY", "DISNEY", "APPLE.COM", "GOOGLE", "YOUTUBE", "MICROSOFT", "AMAZON PRIME", "GITHUB", "OVH"]),
		new Entry("housing", "Housing", CategoryType.Expense, "#bb6bd9", ["RENT", "LOYER", "HUUR", "SYNDIC", "IMMO"]),
		new Entry("insurance", "Insurance", CategoryType.Expense, "#56ccf2", ["VERZEKERING", "ASSURANCE", "INSURANCE", "AXA", "ETHIAS", "BALOISE", "P&V", "DKV"]),
	}.ToDictionary(e => e.Id, StringComparer.Ordinal);
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : One category of the old flat default list.
	/// </summary>
	/// <param name="Id">The category id.</param>
	/// <param name="Name">The name it was seeded with.</param>
	/// <param name="Type">The type it was seeded with.</param>
	/// <param name="Color">The colour it was seeded with.</param>
	/// <param name="Keywords">The keywords it was seeded with, in order.</param>
	internal sealed record Entry(string Id, string Name, CategoryType Type, string Color, string[] Keywords)
	{
		#region Public Methods
		/// <summary>
		/// Determines whether a stored category is still exactly this default: same name, type, colour and keywords.
		/// </summary>
		/// <param name="category">The stored category.</param>
		/// <returns><see langword="true"/> when the owner never edited it.</returns>
		public bool Matches(Category category) =>
			string.Equals(category.Name, Name, StringComparison.Ordinal)
			&& category.Type == Type
			&& string.Equals(category.Color, Color, StringComparison.OrdinalIgnoreCase)
			&& category.Keywords.SequenceEqual(Keywords, StringComparer.Ordinal);
		#endregion
	}
	#endregion
}
