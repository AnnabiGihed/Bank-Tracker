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

		var ownTransfers = categories.Where(c => string.Equals(c.Id, "own-transfers", StringComparison.Ordinal)).ToList();
		ownTransfers.Should().HaveCount(1);
		ownTransfers[0].Keywords.Should().Contain("REVOLUT");
		categories.Single(c => string.Equals(c.Id, "groceries", StringComparison.Ordinal)).Type.Should().Be(CategoryType.Expense);
		categories.Any(c => string.Equals(c.Id, "other", StringComparison.Ordinal) && c.BuiltIn).Should().BeTrue("The analysis needs the built-in 'other' category.");
	}
	#endregion
}
