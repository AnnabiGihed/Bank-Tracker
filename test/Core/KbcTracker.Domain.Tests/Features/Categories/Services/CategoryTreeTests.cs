namespace KbcTracker.Domain.Tests.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoryTree"/>: navigation, the roll-up of amounts, and the rules a change to
///              the tree must keep (parent exists, no cycle, at most four levels, a sub-category has its root's type,
///              built-in categories cannot be deleted).
/// </summary>
public sealed class CategoryTreeTests
{
	#region Test Infrastructure
	/// <summary>
	/// Creates a category for the sample tree.
	/// </summary>
	private static Category Node(string id, string? parent, CategoryType type = CategoryType.Expense, bool builtIn = false) =>
		Category.Create(new CategoryId(id), id, type, "#123456", [], builtIn, parent is null ? null : new CategoryId(parent)).Value;

	/// <summary>
	/// A tree of four levels (a > b > c > d), a second root e with a child f, and an income root i.
	/// </summary>
	private static CategoryTree Sample() => new(
	[
		Node("a", null), Node("b", "a"), Node("c", "b"), Node("d", "c"),
		Node("e", null), Node("f", "e"),
		Node("i", null, CategoryType.Income), Node("other", null, builtIn: true),
	]);
	#endregion

	#region Navigation Tests
	/// <summary>
	/// Verifies parent, children, ancestors, descendants, depth, path and root.
	/// </summary>
	[Fact]
	public void Navigation_FourLevelTree_AnswersEveryQuestion()
	{
		var tree = Sample();

		tree.Parent("c")!.Id.Value.Should().Be("b");
		tree.Parent("a").Should().BeNull();
		tree.Children("a").Select(c => c.Id.Value).Should().Equal("b");
		tree.Ancestors("d").Select(c => c.Id.Value).Should().Equal("c", "b", "a");
		tree.Descendants("a").Select(c => c.Id.Value).Should().Equal("b", "c", "d");
		tree.Depth("d").Should().Be(4);
		tree.Path("d").Select(c => c.Id.Value).Should().Equal("a", "b", "c", "d");
		tree.Root("d")!.Id.Value.Should().Be("a");
		tree.Roots().Select(c => c.Id.Value).Should().Equal("a", "e", "i", "other");
		tree.Contains("zzz").Should().BeFalse();
		tree.Root("zzz").Should().BeNull();
	}

	/// <summary>
	/// Verifies that stored data with a missing parent or a cycle does not hang or throw: the orphan is top level and
	/// the walk stops at the first repeat.
	/// </summary>
	[Fact]
	public void Navigation_MissingParentAndCycle_Tolerated()
	{
		var tree = new CategoryTree([Node("x", "missing"), Node("p", "q"), Node("q", "p")]);

		tree.Roots().Select(c => c.Id.Value).Should().Equal("x");
		tree.Depth("x").Should().Be(1);
		tree.Ancestors("p").Select(c => c.Id.Value).Should().Equal("q");
		tree.Descendants("p").Select(c => c.Id.Value).Should().Equal("q");
	}
	#endregion

	#region RollUp Tests
	/// <summary>
	/// Verifies that a node's total is its own amount plus its descendants' and that siblings stay apart.
	/// </summary>
	[Fact]
	public void RollUp_AmountsOnNodes_SummedThroughAncestors()
	{
		var totals = Sample().RollUp(new Dictionary<string, decimal> { ["a"] = 1m, ["c"] = 10m, ["d"] = 100m, ["f"] = 5m });

		totals["a"].Should().Be(111m);
		totals["b"].Should().Be(110m);
		totals["c"].Should().Be(110m);
		totals["d"].Should().Be(100m);
		totals["e"].Should().Be(5m);
		totals.Should().NotContainKey("i");
	}
	#endregion

	#region Validate Tests
	/// <summary>
	/// Verifies that a valid placement succeeds: top level, under a root with its type, and a move that keeps the depth.
	/// </summary>
	[Fact]
	public void Validate_ValidPlacements_Succeed()
	{
		var tree = Sample();

		tree.Validate(null, null, CategoryType.Savings).IsSuccess.Should().BeTrue();
		tree.Validate(null, "b", CategoryType.Expense).IsSuccess.Should().BeTrue();
		tree.Validate("f", "a", CategoryType.Expense).IsSuccess.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that an unknown parent is refused.
	/// </summary>
	[Fact]
	public void Validate_UnknownParent_ParentNotFound() =>
		Sample().Validate(null, "zzz", CategoryType.Expense).Error.Should().Be(CategoryErrors.ParentNotFound);

	/// <summary>
	/// Verifies that a category cannot go under itself or under its own descendant.
	/// </summary>
	[Fact]
	public void Validate_ParentIsSelfOrDescendant_ParentCycle()
	{
		var tree = Sample();

		tree.Validate("a", "a", CategoryType.Expense).Error.Should().Be(CategoryErrors.ParentCycle);
		tree.Validate("a", "c", CategoryType.Expense).Error.Should().Be(CategoryErrors.ParentCycle);
	}

	/// <summary>
	/// Verifies the four-level cap for a new child and for a move that carries a subtree down.
	/// </summary>
	[Fact]
	public void Validate_MoreThanFourLevels_TooDeep()
	{
		var tree = Sample();

		tree.Validate(null, "d", CategoryType.Expense).Error.Should().Be(CategoryErrors.TooDeep);
		tree.Validate("e", "c", CategoryType.Expense).Error.Should().Be(CategoryErrors.TooDeep, "e carries f: c is level 3, so e would be 4 and f 5");
	}

	/// <summary>
	/// Verifies that a sub-category must have the type of its root.
	/// </summary>
	[Fact]
	public void Validate_TypeDiffersFromRoot_TypeFollowsRoot() =>
		Sample().Validate(null, "a", CategoryType.Income).Error.Should().Be(CategoryErrors.TypeFollowsRoot);

	/// <summary>
	/// Verifies that a built-in category cannot be moved or given another type, while keeping its place and type
	/// (a rename or recolour) is fine, and that a category cannot be retyped when that would retype a built-in below it.
	/// </summary>
	[Fact]
	public void Validate_BuiltInMovedOrRetyped_BuiltInFixed()
	{
		var tree = new CategoryTree(
		[
			Node("a", null), Node("fee", "a", builtIn: true), Node("income", null, CategoryType.Income, builtIn: true), Node("other", null, builtIn: true),
		]);

		tree.Validate("fee", "a", CategoryType.Expense).IsSuccess.Should().BeTrue();
		tree.Validate("fee", null, CategoryType.Expense).Error.Should().Be(CategoryErrors.BuiltInFixed);
		tree.Validate("other", "a", CategoryType.Expense).Error.Should().Be(CategoryErrors.BuiltInFixed);
		tree.Validate("income", null, CategoryType.Expense).Error.Should().Be(CategoryErrors.BuiltInFixed);
		tree.Validate("a", null, CategoryType.Income).Error.Should().Be(CategoryErrors.BuiltInFixed);
	}

	/// <summary>
	/// Verifies that built-in categories, and only they, are protected from deletion.
	/// </summary>
	[Fact]
	public void ValidateDeletion_BuiltIn_Protected()
	{
		var tree = Sample();

		tree.ValidateDeletion("other").Error.Should().Be(CategoryErrors.BuiltInProtected);
		tree.ValidateDeletion("a").IsSuccess.Should().BeTrue();
	}
	#endregion
}
