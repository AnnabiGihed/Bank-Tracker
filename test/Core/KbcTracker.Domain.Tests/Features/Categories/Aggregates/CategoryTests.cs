namespace KbcTracker.Domain.Tests.Features.Categories.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the rules of <see cref="Category"/>: a name and a <c>#rrggbb</c> colour are required, and
///              keywords keep their exact text.
/// </summary>
public sealed class CategoryTests
{
	#region Creation Tests
	/// <summary>
	/// Verifies that blank keywords are dropped but the others keep their exact text, since the trailing space of
	/// "AD " stops it matching "ADIDAS".
	/// </summary>
	[Fact]
	public void Create_Keywords_BlankDroppedTextKept()
	{
		var category = Category.Create(new CategoryId("groceries"), "Groceries", CategoryType.Expense, "#3454d1", ["AD ", " ", "DELHAIZE"], builtIn: false);

		category.Value.Keywords.Should().Equal("AD ", "DELHAIZE");
	}

	/// <summary>
	/// Verifies the errors of a category without name or with a malformed colour.
	/// </summary>
	[Fact]
	public void Create_InvalidValues_Refused()
	{
		Category.Create(new CategoryId("x"), " ", CategoryType.Expense, "#3454d1", [], builtIn: false).Error.Should().Be(CategoryErrors.NameRequired);
		Category.Create(new CategoryId("x"), "X", CategoryType.Expense, "blue", [], builtIn: false).Error.Should().Be(CategoryErrors.ColorInvalid);
	}
	#endregion

	#region Behaviour Tests
	/// <summary>
	/// Verifies that an update changes name, type, colour and keywords, and that a refused update changes nothing.
	/// </summary>
	[Fact]
	public void Update_ValidThenInvalid_OnlyValidApplied()
	{
		var category = Category.Create(new CategoryId("car"), "Car", CategoryType.Expense, "#3454d1", ["TOTAL"], builtIn: false).Value;

		category.Update("Car & fuel", CategoryType.Expense, "#123456", ["TOTAL", "Q8"], null).IsSuccess.Should().BeTrue();
		category.Update("", CategoryType.Income, "#000000", [], null).IsFailure.Should().BeTrue();

		category.Name.Should().Be("Car & fuel");
		category.Type.Should().Be(CategoryType.Expense);
		category.Keywords.Should().Equal("TOTAL", "Q8");
	}

	/// <summary>
	/// Verifies that an update can move a category under a parent and that passing the category's own keyword list
	/// back in keeps the keywords.
	/// </summary>
	[Fact]
	public void Update_ParentAndOwnKeywords_ParentSetKeywordsKept()
	{
		var category = Category.Create(new CategoryId("car"), "Car", CategoryType.Expense, "#3454d1", ["TOTAL", "Q8"], builtIn: false, coicop: "07.2").Value;

		category.Update(category.Name, category.Type, category.Color, category.Keywords, new CategoryId("transport")).IsSuccess.Should().BeTrue();

		category.ParentId.Should().Be(new CategoryId("transport"));
		category.Coicop.Should().Be("07.2");
		category.Keywords.Should().Equal("TOTAL", "Q8");
	}
	#endregion
}
