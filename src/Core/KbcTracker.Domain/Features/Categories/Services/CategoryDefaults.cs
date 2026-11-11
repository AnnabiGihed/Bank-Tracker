using System.Text;

namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Creates the first-run category list: the built-in categories the analysis relies on, plus the
///              user's own categories built from a name → keywords seed.
///
///              Shared by every store implementation so the web host and the Android app start with the same
///              categories. The default seed lives here, in code, because it is written once into an empty database
///              and then belongs to the user, who edits it on the Categories tab; it is not a setting.
/// </summary>
public static class CategoryDefaults
{
	#region Static Instances
	/// <summary>
	/// The first-run seed: category names with the keywords looked for in payee and shop names. The order sets the
	/// colours and the trailing spaces in <c>"AD "</c> and <c>"CM "</c> are deliberate: they stop the keyword from
	/// matching words that only start with it, such as ADIDAS or CMS.
	/// </summary>
	private static readonly Dictionary<string, string[]> DefaultSeed = new(StringComparer.Ordinal)
	{
		["Own transfers"] = ["REVOLUT", "WISE", "N26"],
		["Car"] = ["AUTOMOBILE", "TOYOTA", "GARAGE", "CARWASH", "FITWASH", "D'IETEREN", "VROOM"],
		["Groceries"] = ["DELHAIZE", "COLRUYT", "CARREFOUR", "ALDI", "LIDL", "AD ", "OKAY", "SPAR", "INTERMARCHE", "ALBERT HEIJN", "CORA", "MATCH", "PROXY"],
		["Restaurants & cafes"] = ["RESTAURANT", "CAFE", "MCDONALD", "QUICK", "BURGER", "PIZZA", "DELIVEROO", "UBER EATS", "UBER *EATS", "TAKEAWAY", "FEDOREST", "EXKI", "PAUL"],
		["Transport & fuel"] = ["STIB", "MIVB", "SNCB", "NMBS", "DE LIJN", "TEC", "EUROSTAR", "SNCF", "OUIGO", "RATP", "FLIXBUS", "TOTAL", "Q8", "SHELL", "ESSO", "LUKOIL", "UBER", "BOLT", "PARKING", "INDIGO", "INTERPARKING"],
		["Shopping"] = ["AMAZON", "AMZN", "BOL.COM", "ZALANDO", "COOLBLUE", "MEDIA MARKT", "MEDIAMARKT", "IKEA", "ACTION", "HEMA", "PRIMARK", "DECATHLON", "FNAC", "KRUIDVAT"],
		["Health"] = ["PHARMACIE", "APOTHEEK", "MUTUALITE", "MUTUALITEIT", "PARTENA", "HELAN", "SOLIDARIS", "CM ", "DOKTER", "DOCTEUR"],
		["Utilities & telecom"] = ["ENGIE", "ELECTRABEL", "LUMINUS", "TOTALENERGIES", "VIVAQUA", "SIBELGA", "PROXIMUS", "ORANGE", "TELENET", "BASE", "VOO", "SCARLET", "MOBILE VIKINGS"],
		["Subscriptions"] = ["NETFLIX", "SPOTIFY", "DISNEY", "APPLE.COM", "GOOGLE", "YOUTUBE", "MICROSOFT", "AMAZON PRIME", "GITHUB", "OVH"],
		["Cash"] = ["GELDOPNAME", "RETRAIT", "CASH", "ATM", "BANCONTACT GELDAUTOMAAT"],
		["Housing"] = ["RENT", "LOYER", "HUUR", "SYNDIC", "IMMO"],
		["Insurance"] = ["VERZEKERING", "ASSURANCE", "INSURANCE", "AXA", "ETHIAS", "BALOISE", "P&V", "DKV"],
		["Bank & fees"] = ["KOSTEN", "FRAIS BANCAIRES"],
	};

	/// <summary>
	/// Colours given in turn to seeded categories that have no built-in colour.
	/// </summary>
	private static readonly string[] Palette =
		["#3454d1", "#f2994a", "#27ae60", "#9b51e0", "#eb5757", "#2d9cdb", "#f2c94c", "#6fcf97", "#bb6bd9", "#56ccf2", "#219653", "#e67e22", "#16a085", "#c0392b", "#8e44ad"];
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates the categories a new database starts with: the built-in ones plus the default seed.
	/// </summary>
	/// <returns>The new category list; the built-in <c>other</c> category is always included.</returns>
	public static IList<Category> CreateDefaults() => Create(DefaultSeed);

	/// <summary>
	/// Creates the built-in categories and adds one category per seed entry. A seed entry whose name or slug
	/// matches a built-in category adds its keywords to that category instead; one whose name has no letter or digit
	/// is skipped.
	/// </summary>
	/// <param name="seed">Category names with the keywords looked for in payee and shop names.</param>
	/// <returns>The new category list; the built-in <c>other</c> category is always included.</returns>
	public static IList<Category> Create(IReadOnlyDictionary<string, string[]> seed)
	{
		var drafts = new List<Draft>
		{
			new("income", "Income", CategoryType.Income, "#27ae60", BuiltIn: true),
			new("refunds", "Refunds", CategoryType.Income, "#6fcf97", BuiltIn: true),
			new("savings", Analysis.SavingsName, CategoryType.Savings, "#9b51e0", BuiltIn: true),
			new("own-transfers", Analysis.OwnTransfersName, CategoryType.Transfer, "#bdbdbd", BuiltIn: true),
			new("loan", "Loan", CategoryType.Expense, "#c0392b", BuiltIn: true),
			new("bills", "Bills & direct debits", CategoryType.Expense, "#2d9cdb", BuiltIn: true),
			new("bank-fees", "Bank & fees", CategoryType.Expense, "#828282", BuiltIn: true),
			new("cash", "Cash", CategoryType.Expense, "#f2c94c", BuiltIn: true),
			new("transfers", "Transfers to others", CategoryType.Expense, "#56ccf2", BuiltIn: true),
			new(CategoryId.Other.Value, "Other", CategoryType.Expense, "#a0a7b4", BuiltIn: true),
		};
		var colourIndex = 0;
		foreach (var (name, words) in seed)
		{
			var id = Slug(name);
			var existing = drafts.Find(c => string.Equals(c.Id, id, StringComparison.Ordinal) || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (existing is not null)
			{
				existing.Keywords.AddRange(words);
				continue;
			}

			if (id.Length == 0)
			{
				continue;
			}

			var type = name.Equals(Analysis.OwnTransfersName, StringComparison.OrdinalIgnoreCase) ? CategoryType.Transfer : CategoryType.Expense;
			var draft = new Draft(id, name, type, Palette[colourIndex++ % Palette.Length], BuiltIn: false);
			draft.Keywords.AddRange(words);
			drafts.Add(draft);
		}

		return drafts
			.Select(d => Category.Create(new CategoryId(d.Id), d.Name, d.Type, d.Color, d.Keywords, d.BuiltIn))
			.Where(r => r.IsSuccess)
			.Select(r => r.Value)
			.ToList();
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Turns a category name into a stable id: lower case, runs of other characters replaced by one dash
	/// (<c>"Transport &amp; fuel"</c> → <c>"transport-fuel"</c>). An accent is split off its letter and also becomes
	/// a dash (<c>"cafés"</c> → <c>"cafe-s"</c>); ids are stored in the user's data, so this must not change.
	/// </summary>
	/// <param name="name">The category name.</param>
	/// <returns>The id; empty when the name has no letters or digits.</returns>
	public static string Slug(string name)
	{
		var slug = new StringBuilder();
		foreach (var ch in name.ToLowerInvariant().Normalize(NormalizationForm.FormD))
		{
			if (char.IsLetterOrDigit(ch))
			{
				slug.Append(ch);
			}
			else if (slug.Length > 0 && slug[^1] != '-')
			{
				slug.Append('-');
			}
		}

		return slug.ToString().Trim('-');
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : A category being assembled from the built-in list and the seed, before it is validated.
	/// </summary>
	/// <param name="Id">The category id.</param>
	/// <param name="Name">The name.</param>
	/// <param name="Type">How its payments count in the totals.</param>
	/// <param name="Color">The colour, as <c>#rrggbb</c>.</param>
	/// <param name="BuiltIn">Whether the analysis relies on it.</param>
	private sealed record Draft(string Id, string Name, CategoryType Type, string Color, bool BuiltIn)
	{
		#region Properties
		/// <summary>
		/// Gets the keywords gathered so far.
		/// </summary>
		public List<string> Keywords { get; } = [];
		#endregion
	}
	#endregion
}
