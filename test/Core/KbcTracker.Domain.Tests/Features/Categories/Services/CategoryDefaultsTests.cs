namespace KbcTracker.Domain.Tests.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the default category tree and the category ids from <see cref="CategoryDefaults"/>.
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
	#endregion

	#region CreateDefaults Tests
	/// <summary>
	/// Verifies the default tree a new database starts with: unique ids, every built-in present, kept ids in their
	/// place, and the deliberate trailing spaces kept.
	/// </summary>
	[Fact]
	public void CreateDefaults_Belgium_TreeWithBuiltInsAndKeptIds()
	{
		var categories = CategoryDefaults.CreateDefaults("BE");

		categories.Should().OnlyHaveUniqueItems(c => c.Id);
		categories.Select(c => c.Id.Value).Should().Contain(
			["other", "income", "refunds", "savings", "own-transfers", "loan-received", "loan", "bills", "bank-fees", "cash", "transfers", "groceries", "car", "housing", "car-fuel"]);
		categories.Where(c => c.BuiltIn).Select(c => c.Id.Value).Should().HaveCount(11).And.Contain(
			["income", "refunds", "savings", "own-transfers", "loan-received", "loan", "bills", "bank-fees", "cash", "transfers", "other"]);
		categories.Single(c => c.Id == new CategoryId("car-fuel")).ParentId.Should().Be(new CategoryId("car"));
		categories.Single(c => c.Id == new CategoryId("car-fuel")).Coicop.Should().Be("07.2");
		categories.Single(c => c.Id == new CategoryId("groceries")).Keywords.Should().Contain("AD ");
		categories.Single(c => c.Id == new CategoryId("own-transfers")).Type.Should().Be(CategoryType.Transfer);
		categories.Single(c => c.Id == CategoryId.LoanReceived).Type.Should().Be(CategoryType.Transfer);
		categories.Single(c => c.Id == CategoryId.LoanReceived).ParentId.Should().BeNull();
	}

	/// <summary>
	/// Verifies the stand-in for a database stored before the built-in existed: a protected, top-level Transfer.
	/// </summary>
	[Fact]
	public void CreateLoanReceived_OldTreeWithoutIt_IsProtectedTransfer()
	{
		var old = CategoryDefaults.CreateDefaults("BE").Where(c => c.Id != CategoryId.LoanReceived).ToList();
		var restored = CategoryDefaults.CreateLoanReceived();
		var tree = new CategoryTree([.. old, restored]);

		restored.Type.Should().Be(CategoryType.Transfer);
		restored.BuiltIn.Should().BeTrue();
		tree.Roots().Should().Contain(restored);
		tree.ValidateDeletion(CategoryId.LoanReceived.Value).IsFailure.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that the tree is well formed: at most four levels, every parent exists, every child has its root's
	/// type.
	/// </summary>
	[Fact]
	public void CreateDefaults_Tree_DepthTypeAndParentsConsistent()
	{
		var categories = CategoryDefaults.CreateDefaults("BE");
		var tree = new CategoryTree(categories);

		foreach (var category in categories)
		{
			tree.Depth(category.Id.Value).Should().BeLessThanOrEqualTo(CategoryTree.MaxDepth, category.Id.Value);
			(category.ParentId is null || tree.Contains(category.ParentId.Value)).Should().BeTrue(category.Id.Value);
			category.Type.Should().Be(tree.Root(category.Id.Value)!.Type, category.Id.Value);
		}

		tree.Depth("car-fuel").Should().Be(4);
	}

	/// <summary>
	/// Verifies that the Belgian pack is loaded only for Belgium, in any letter case, while the worldwide core is
	/// always there.
	/// </summary>
	/// <param name="country">The country code.</param>
	/// <param name="belgianPack">Whether the Belgian keywords are expected.</param>
	[Theory]
	[InlineData("BE", true)]
	[InlineData("be", true)]
	[InlineData("FR", false)]
	[InlineData("", false)]
	public void CreateDefaults_Country_BelgianPackOnlyForBelgium(string country, bool belgianPack)
	{
		var categories = CategoryDefaults.CreateDefaults(country);

		var groceries = categories.Single(c => c.Id == new CategoryId("groceries")).Keywords;
		groceries.Should().Contain("LIDL");
		groceries.Contains("DELHAIZE").Should().Be(belgianPack);
		categories.Single(c => c.Id == new CategoryId("transport-public")).Keywords.Contains("STIB").Should().Be(belgianPack);
	}

	/// <summary>
	/// Verifies that the trailing space of the deliberate keywords survives in the seed.
	/// </summary>
	[Fact]
	public void CreateDefaults_Belgium_TrailingSpaceKeywordsKept()
	{
		var categories = CategoryDefaults.CreateDefaults("BE");

		categories.SelectMany(c => c.Keywords).Should().Contain(["AD ", "CM ", "BAR ", "CHU ", "UZ ", "DI ", "VOI "]);
		categories.SelectMany(c => c.Keywords).Should().NotContain(["AD", "CM", "BAR"]);
	}
	#endregion
}
