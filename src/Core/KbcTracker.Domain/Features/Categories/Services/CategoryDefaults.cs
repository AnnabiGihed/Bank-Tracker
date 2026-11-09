using System.Text;

namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Creates the first-run category list: the built-in categories the analysis relies on, plus the
///              user's own categories built from a name → keywords seed.
///
///              Shared by every store implementation so the web host and the Android app start with the same
///              categories. The seed comes from appsettings.json today; it moves into the database in step 2.
/// </summary>
public static class CategoryDefaults
{
	#region Static Instances
	/// <summary>
	/// Colours given in turn to seeded categories that have no built-in colour.
	/// </summary>
	private static readonly string[] Palette =
		["#3454d1", "#f2994a", "#27ae60", "#9b51e0", "#eb5757", "#2d9cdb", "#f2c94c", "#6fcf97", "#bb6bd9", "#56ccf2", "#219653", "#e67e22", "#16a085", "#c0392b", "#8e44ad"];
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates the built-in categories and adds one category per seed entry. A seed entry whose name or slug
	/// matches a built-in category adds its keywords to that category instead.
	/// </summary>
	/// <param name="seed">Category names with the keywords looked for in payee and shop names.</param>
	/// <returns>The new category list; the built-in <c>other</c> category is always included.</returns>
	public static IList<Category> Create(IReadOnlyDictionary<string, string[]> seed)
	{
		var list = new List<Category>
		{
			new() { Id = "income", Name = "Income", Type = CategoryType.Income, Color = "#27ae60", BuiltIn = true },
			new() { Id = "refunds", Name = "Refunds", Type = CategoryType.Income, Color = "#6fcf97", BuiltIn = true },
			new() { Id = "savings", Name = Analysis.SavingsName, Type = CategoryType.Savings, Color = "#9b51e0", BuiltIn = true },
			new() { Id = "own-transfers", Name = Analysis.OwnTransfersName, Type = CategoryType.Transfer, Color = "#bdbdbd", BuiltIn = true },
			new() { Id = "loan", Name = "Loan", Type = CategoryType.Expense, Color = "#c0392b", BuiltIn = true },
			new() { Id = "bills", Name = "Bills & direct debits", Type = CategoryType.Expense, Color = "#2d9cdb", BuiltIn = true },
			new() { Id = "bank-fees", Name = "Bank & fees", Type = CategoryType.Expense, Color = "#828282", BuiltIn = true },
			new() { Id = "cash", Name = "Cash", Type = CategoryType.Expense, Color = "#f2c94c", BuiltIn = true },
			new() { Id = "transfers", Name = "Transfers to others", Type = CategoryType.Expense, Color = "#56ccf2", BuiltIn = true },
			new() { Id = "other", Name = "Other", Type = CategoryType.Expense, Color = "#a0a7b4", BuiltIn = true },
		};
		var colourIndex = 0;
		foreach (var (name, words) in seed)
		{
			var id = Slug(name);
			var existing = list.Find(c => string.Equals(c.Id, id, StringComparison.Ordinal) || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (existing is not null)
			{
				foreach (var word in words)
				{
					existing.Keywords.Add(word);
				}

				continue;
			}

			list.Add(new Category
			{
				Id = id,
				Name = name,
				Keywords = [.. words],
				Color = Palette[colourIndex++ % Palette.Length],
				Type = name.Equals(Analysis.OwnTransfersName, StringComparison.OrdinalIgnoreCase) ? CategoryType.Transfer : CategoryType.Expense
			});
		}

		return list;
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
}
