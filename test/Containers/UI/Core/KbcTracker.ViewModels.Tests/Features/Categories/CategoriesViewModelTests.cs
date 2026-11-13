namespace KbcTracker.ViewModels.Tests.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoriesViewModel"/> as data: the tree per type with totals rolled up through the
///              sub-categories, the bars, the comparison with the previous months, which rows are shown, and the form
///              that creates, moves, edits and deletes categories.
/// </summary>
public sealed class CategoriesViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds September with 5 euro in Trips, and October with 8 euro in Rail trips (a child of Trips) and 2 euro in Trips itself.</summary>
	private static Analysis TwoMonths() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("s1", new DateOnly(2026, 9, 5), -5m, "Kiosk"),
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 6), -2m, "Kiosk"),
	], (user, first) =>
	{
		user.AssignPayeeCategory(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"));
		user.AssignPayeeCategory(TestWorkspace.SeriesKeyOf(first, "t2"), new CategoryId("trav"));
	});

	/// <summary>Finds a row of the Spending card.</summary>
	private static CategoryRow Row(CategoriesViewModel categories, string id) =>
		categories.Cards.Single(c => c.Type == CategoryType.Expense).Rows.Single(r => string.Equals(r.Id, id, StringComparison.Ordinal));
	#endregion

	#region Total Tests
	/// <summary>Verifies a parent's total rolls up its sub-categories, the child follows its parent, and the card adds up the top level only.</summary>
	[Fact]
	public void Cards_RollUp_ParentSumsChildAndChildFollowsParent()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);

		var spending = categories.Cards.Single(c => c.Type == CategoryType.Expense);
		var ids = spending.Rows.Select(r => r.Id).ToList();
		Row(categories, "trav").Value.Should().Be(10m);
		Row(categories, "trav").ValueText.Should().Be("10,00 €");
		Row(categories, "trav-train").Value.Should().Be(8m);
		Row(categories, "trav-train").Depth.Should().Be(2);
		Row(categories, "trav-train").ParentId.Should().Be("trav");
		ids.IndexOf("trav-train").Should().Be(ids.IndexOf("trav") + 1);
		spending.Title.Should().Be("Spending");
		spending.Total.Should().Be(10m);
		spending.TotalText.Should().Be("10,00 €");
		categories.MonthLabel.Should().Be("October 2026");
		categories.Cards.Select(c => c.Type).Should().BeInAscendingOrder();
	}

	/// <summary>Verifies a category without spending shows a dash, an empty bar and no comparison, and the bars are relative to the largest top-level total.</summary>
	[Fact]
	public void Rows_BarAndDash_RelativeToLargestTopLevel()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);

		Row(categories, "trav").BarPercent.Should().Be(100m);
		Row(categories, "trav-train").BarPercent.Should().Be(80m);
		Row(categories, "bills").ValueText.Should().Be("—");
		Row(categories, "bills").BarPercent.Should().Be(0m);
		Row(categories, "trav").PaymentsText.Should().Be("3 payment(s) in total");
		Row(categories, "trav").HasChildren.Should().BeTrue();
		Row(categories, "trav-train").HasChildren.Should().BeFalse();
	}

	/// <summary>Verifies the comparison with the previous months: higher by more than 15 percent, lower, similar, and a dash without earlier months.</summary>
	[Fact]
	public void Comparison_AgainstPreviousMonths_HigherLowerSimilarOrNone()
	{
		var october = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);
		var september = new CategoriesViewModel(TwoMonths(), "2026-09", Formats.Belgian);

		Row(october, "trav").Comparison.Should().Be(new CategoryComparison(ComparisonKind.Higher, "+5 €"));
		Row(september, "trav").Comparison.Should().Be(new CategoryComparison(ComparisonKind.None, "—"));
		CategoryComparison.Of(5m, 10m, CategoryType.Expense, Formats.Belgian).Should().Be(new CategoryComparison(ComparisonKind.Lower, "−5 €"));
		CategoryComparison.Of(10.5m, 10m, CategoryType.Expense, Formats.Belgian).Kind.Should().Be(ComparisonKind.Similar);
		CategoryComparison.Of(10.5m, 10m, CategoryType.Income, Formats.Belgian).Kind.Should().Be(ComparisonKind.None);
		CategoryComparison.Of(10m, 0m, CategoryType.Expense, Formats.Belgian).Kind.Should().Be(ComparisonKind.None);
	}
	#endregion

	#region Expansion Tests
	/// <summary>Verifies sub-categories start hidden, show when their parent is expanded, and hide again.</summary>
	[Fact]
	public void VisibleRows_SubCategoriesStartHiddenUntilToggled()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);
		var card = categories.Cards.Single(c => c.Type == CategoryType.Expense);

		var collapsed = categories.VisibleRows(card).Select(r => r.Id).ToList();
		categories.Toggle("trav");
		var expanded = categories.VisibleRows(card).Select(r => r.Id).ToList();
		categories.Toggle("trav");

		collapsed.Should().NotContain("trav-train").And.Contain("trav");
		expanded.Should().Contain("trav-train");
		expanded.IndexOf("trav-train").Should().Be(expanded.IndexOf("trav") + 1);
		categories.VisibleRows(card).Select(r => r.Id).Should().NotContain("trav-train");
		categories.IsExpanded("trav").Should().BeFalse();
	}
	#endregion

	#region Form Tests
	/// <summary>Verifies a new sub-category starts with its parent's colour, takes the type of its root, and sends the old request shape.</summary>
	[Fact]
	public void NewCategory_SubCategory_ParentColourAndTypeFollowsRoot()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);

		var form = categories.NewCategory("trav");
		form.Name = "Bus";
		form.Keywords = "DE LIJN, TEC";

		form.Title.Should().Be("New sub-category");
		form.IsNew.Should().BeTrue();
		form.ParentId.Should().Be("trav");
		form.Color.Should().Be("#3454d1");
		form.ShowType.Should().BeFalse();
		form.ShowParent.Should().BeTrue();
		form.CanDelete.Should().BeFalse();
		form.ToRequest().Should().Be(new DecisionRequest("saveCategory", Id: null, Name: "Bus", Type: "", ParentId: "trav", Color: "#3454d1", Keywords: "DE LIJN, TEC"));
	}

	/// <summary>Verifies a new top-level category chooses its own type and needs a name.</summary>
	[Fact]
	public void NewCategory_TopLevel_ChoosesItsTypeAndNeedsAName()
	{
		var form = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian).NewCategory();

		var withoutName = () => form.ToRequest();
		withoutName.Should().Throw<InvalidOperationException>();
		form.Name = "Hobbies";
		form.Type = CategoryType.Savings;

		form.Title.Should().Be("New category");
		form.ShowType.Should().BeTrue();
		form.ToRequest().Type.Should().Be("Savings");
		form.ToRequest().ParentId.Should().BeEmpty();
	}

	/// <summary>Verifies a built-in category keeps its parent and type when saved and cannot be deleted, while another one can.</summary>
	[Fact]
	public void Edit_BuiltIn_FixedParentAndTypeNotDeletable()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);
		var other = categories.Cards.SelectMany(c => c.Rows).Single(r => string.Equals(r.Id, "other", StringComparison.Ordinal));

		var form = categories.Edit(other);
		var delete = () => form.DeleteRequest();

		form.BuiltIn.Should().BeTrue();
		form.ShowParent.Should().BeFalse();
		form.ShowType.Should().BeFalse();
		form.CanDelete.Should().BeFalse();
		delete.Should().Throw<InvalidOperationException>();
		var request = form.ToRequest();
		request.Id.Should().Be("other");
		request.ParentId.Should().BeNull();
		request.Type.Should().Be(other.Type.ToString());
	}

	/// <summary>Verifies the parent choices leave out the category itself and everything below it, and nothing so deep that its own children would pass the deepest level.</summary>
	[Fact]
	public void Edit_ParentOptions_NotItselfNorBelowAndNotTooDeep()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);
		var trips = Row(categories, "trav");

		var form = categories.Edit(trips);

		form.ParentOptions.Select(o => o.Id).Should().NotContain(["trav", "trav-train"]);
		form.ParentOptions.Should().NotBeEmpty().And.OnlyContain(o => o.Depth + 2 <= CategoryTree.MaxDepth);
		categories.NewCategory().ParentOptions.Should().Contain(o => o.Depth == CategoryTree.MaxDepth - 1);
		categories.NewCategory().ParentOptions.Should().NotContain(o => o.Depth == CategoryTree.MaxDepth);
	}

	/// <summary>Verifies the delete question says where the payments go, to the parent or, for a top-level category, to "Other", and builds the delete request.</summary>
	[Fact]
	public void Delete_PromptAndRequest_ParentOrOther()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);

		var child = categories.Edit(Row(categories, "trav-train"));
		var top = categories.Edit(Row(categories, "trav"));

		child.CanDelete.Should().BeTrue();
		child.DeletePrompt().Should().Contain("its parent, “Trips”.");
		top.DeletePrompt().Should().Contain("“Other”");
		child.DeleteRequest().Should().Be(new DecisionRequest("deleteCategory", Id: "trav-train"));
	}

	/// <summary>Verifies the form starts from the category's values, and a row says whether a sub-category can be added.</summary>
	[Fact]
	public void Edit_ExistingCategory_StartsFromItsValues()
	{
		var categories = new CategoriesViewModel(TwoMonths(), "2026-10", Formats.Belgian);

		var form = categories.Edit(Row(categories, "trav-train"));

		form.Title.Should().Be("Edit category");
		form.Name.Should().Be("Rail trips");
		form.ParentId.Should().Be("trav");
		form.Color.Should().Be("#56ccf2");
		form.IsNew.Should().BeFalse();
		Row(categories, "trav").CanAddSubCategory.Should().BeTrue();
		new CategoryRow("x", "", "X", "#000", CategoryTree.MaxDepth, false, CategoryType.Expense, false, "", 0, 0, "—", 0, new CategoryComparison(ComparisonKind.None, "—"))
			.CanAddSubCategory.Should().BeFalse();
	}
	#endregion
}
