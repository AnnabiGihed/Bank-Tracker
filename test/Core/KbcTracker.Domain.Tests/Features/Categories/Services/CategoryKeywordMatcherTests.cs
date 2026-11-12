namespace KbcTracker.Domain.Tests.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoryKeywordMatcher"/>: keywords match at the start of a word, the longest
///              keyword wins, the deeper category wins a tie, and the search can be limited to one category type.
/// </summary>
public sealed class CategoryKeywordMatcherTests
{
	#region Test Infrastructure
	/// <summary>
	/// Creates a category with keywords for the sample tree.
	/// </summary>
	private static Category Node(string id, string? parent, string[] keywords, CategoryType type = CategoryType.Expense) =>
		Category.Create(new CategoryId(id), id, type, "#123456", keywords, builtIn: false, parent is null ? null : new CategoryId(parent)).Value;

	/// <summary>
	/// Builds a matcher over a small tree: a root, a child and a grandchild, plus an income root.
	/// </summary>
	private static CategoryKeywordMatcher Sample()
	{
		var categories = new List<Category>
		{
			Node("food", null, ["CARREFOUR", "AD "]),
			Node("cars", null, ["AUTOMOBILE", "SHELL"]),
			Node("fuel", "cars", ["SHELL", "ESSO"]),
			Node("pay", null, ["SALARY"], CategoryType.Income),
		};
		return new CategoryKeywordMatcher(categories, new CategoryTree(categories));
	}
	#endregion

	#region Find Tests
	/// <summary>
	/// Verifies the match starts at a word: a keyword inside a longer word does not match, and one with a trailing
	/// space keeps its meaning.
	/// </summary>
	/// <param name="text">The text searched.</param>
	/// <param name="expected">The expected category, or <see langword="null"/> for none.</param>
	[Theory]
	[InlineData("AD DELHAIZE", "food")]
	[InlineData("AZAD KEBAB", null)]
	[InlineData("carrefour market", "food")]
	[InlineData("XCARREFOUR", null)]
	public void Find_StartOfWord_OnlyWholeWordStartsMatch(string text, string? expected) =>
		Sample().Find(text).Should().Be(expected);

	/// <summary>
	/// Verifies the longest keyword wins, and that equal keywords go to the deeper category.
	/// </summary>
	[Fact]
	public void Find_LongestKeywordThenDeeperCategory_Wins()
	{
		var matcher = Sample();

		matcher.Find("AUTOMOBILE SHELL").Should().Be("cars");
		matcher.Find("SHELL STATION").Should().Be("fuel");
		matcher.Find("CARREFOUR AUTOMOBILE").Should().Be("cars");
	}

	/// <summary>
	/// Verifies that the search can be limited to one type of category.
	/// </summary>
	[Fact]
	public void Find_OnlyType_IgnoresOtherTypes()
	{
		var matcher = Sample();

		matcher.Find("SALARY MARCH", CategoryType.Expense).Should().BeNull();
		matcher.Find("SALARY MARCH", CategoryType.Income).Should().Be("pay");
	}
	#endregion
}
