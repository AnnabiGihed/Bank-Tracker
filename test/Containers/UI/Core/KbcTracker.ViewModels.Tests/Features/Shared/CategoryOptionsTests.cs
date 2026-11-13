namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoryOptions"/>: the pickers list a parent before its child, the child indented,
///              grouped by type, and the split editor's list holds spending categories only.
/// </summary>
public sealed class CategoryOptionsTests
{
	#region Picker Tests
	/// <summary>Verifies the grouped picker lists a parent before its child, the child indented with "— ", and the groups by type.</summary>
	[Fact]
	public void Grouped_ParentBeforeChildIndented_ByType()
	{
		var analysis = TestWorkspace.Analyse([]);

		var groups = CategoryOptions.Grouped(analysis, Formats.Belgian);

		groups.Select(g => g.Type).Should().BeInAscendingOrder();
		groups.Select(g => g.Label).Should().Contain(["Spending", "Income"]);
		var labels = groups.Single(g => g.Type == CategoryType.Expense).Options.Select(o => o.Label).ToList();
		labels.IndexOf("Trips").Should().BeGreaterThanOrEqualTo(0);
		labels.IndexOf("— Rail trips").Should().Be(labels.IndexOf("Trips") + 1);
	}

	/// <summary>Verifies the split editor's list holds spending categories only.</summary>
	[Fact]
	public void Spending_OnlyExpenseCategories()
	{
		var analysis = TestWorkspace.Analyse([]);

		var options = CategoryOptions.Spending(analysis, Formats.Belgian);

		options.Should().NotBeEmpty().And.OnlyContain(o => o.Type == CategoryType.Expense);
		options.Select(o => o.Id).Should().Contain(["trav", "trav-train", "bills"]);
	}

	/// <summary>Verifies a category's ancestors are listed after its own id, and the indent grows by level.</summary>
	[Fact]
	public void WithAncestorsAndIndent_ChildListsParent()
	{
		var analysis = TestWorkspace.Analyse([]);

		CategoryOptions.WithAncestors(analysis, analysis.CategoryById("trav-train")).Should().Equal("trav-train", "trav");
		CategoryOptions.Indent(1).Should().BeEmpty();
		CategoryOptions.Indent(3).Should().Be("— — ");
	}
	#endregion
}
