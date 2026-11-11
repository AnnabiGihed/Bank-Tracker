namespace KbcTracker.Domain.Tests.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the first-run categories and the category ids from <see cref="CategoryDefaults"/>.
///              The ids are stored in the user's data, so <see cref="CategoryDefaults.Slug"/> must keep producing
///              the same id for the same name.
/// </summary>
public sealed class CategoryDefaultsTests
{
	#region Slug Tests
	/// <summary>
	/// Verifies that a name becomes a lower-case id with single dashes. The accent case pins the existing
	/// behaviour (an accent becomes a dash), because ids already stored must stay valid.
	/// </summary>
	/// <param name="name">The category name.</param>
	/// <param name="expected">The expected id.</param>
	[Theory]
	[InlineData("Restaurants & cafés", "restaurants-cafe-s")]
	[InlineData("Transport & fuel", "transport-fuel")]
	[InlineData("  Utilities & telecom ", "utilities-telecom")]
	[InlineData("Own transfers", "own-transfers")]
	[InlineData("&&", "")]
	public void Slug_Name_StableId(string name, string expected)
	{
		CategoryDefaults.Slug(name).Should().Be(expected);
	}
	#endregion

	#region Create Tests
	/// <summary>
	/// Verifies that the built-in categories exist and that a seed entry named like a built-in one adds its
	/// keywords to it instead of creating a duplicate.
	/// </summary>
	[Fact]
	public void Create_SeedMatchingBuiltIn_KeywordsMergedNoDuplicate()
	{
		var categories = CategoryDefaults.Create(new Dictionary<string, string[]>
		{
			["Own transfers"] = ["REVOLUT"],
			["Groceries"] = ["DELHAIZE"],
		});

		var ownTransfers = categories.Where(c => string.Equals(c.Id.Value, "own-transfers", StringComparison.Ordinal)).ToList();
		ownTransfers.Should().HaveCount(1);
		ownTransfers[0].Keywords.Should().Contain("REVOLUT");
		categories.Single(c => string.Equals(c.Id.Value, "groceries", StringComparison.Ordinal)).Type.Should().Be(CategoryType.Expense);
		categories.Any(c => string.Equals(c.Id.Value, "other", StringComparison.Ordinal) && c.BuiltIn).Should().BeTrue("The analysis needs the built-in 'other' category.");
	}

	/// <summary>
	/// Verifies that a seed entry whose name has no letter or digit is skipped: it would have no id.
	/// </summary>
	[Fact]
	public void Create_SeedWithoutLettersOrDigits_Skipped()
	{
		var categories = CategoryDefaults.Create(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["&&"] = ["X"] });

		categories.Should().NotContain(c => c.Name == "&&");
		categories.Should().Contain(c => c.Id == CategoryId.Other);
	}

	/// <summary>
	/// Verifies the default seed a new database starts with: the built-in categories plus the seeded ones, with their
	/// stable ids, the seed's bank-fee words merged into the built-in category, and the deliberate trailing spaces kept.
	/// </summary>
	[Fact]
	public void CreateDefaults_NewDatabase_BuiltInsAndSeededCategories()
	{
		var categories = CategoryDefaults.CreateDefaults();

		categories.Select(c => c.Id.Value).Should().Contain(["other", "income", "savings", "groceries", "restaurants-cafes", "transport-fuel", "own-transfers"]);
		categories.Should().OnlyHaveUniqueItems(c => c.Id);
		categories.Single(c => c.Id == new CategoryId("bank-fees")).Keywords.Should().Contain(["KOSTEN", "FRAIS BANCAIRES"]);
		categories.Single(c => c.Id == new CategoryId("groceries")).Keywords.Should().Contain("AD ");
		categories.Single(c => c.Id == new CategoryId("own-transfers")).Type.Should().Be(CategoryType.Transfer);
	}
	#endregion
}
