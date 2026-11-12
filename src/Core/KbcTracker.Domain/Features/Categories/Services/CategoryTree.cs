namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The category tree: navigation over the parent links of a category list, the roll-up of amounts to
///              every ancestor, and the rules a change to the tree must keep (the parent exists, no cycle, at most
///              <see cref="MaxDepth"/> levels, a sub-category has the type of its top-level category).
///
///              Built from a list of categories and never stored: the parent links on the categories are the only
///              source of truth. Stored data is never trusted to be well formed, so navigation tolerates a parent
///              that does not exist (the node counts as top level) and a cycle (it stops at the first repeat).
/// </summary>
public sealed class CategoryTree
{
	#region Constants
	/// <summary>
	/// The most levels the tree can have: a top-level category is level one.
	/// </summary>
	public const int MaxDepth = 4;
	#endregion

	#region Fields
	/// <summary>
	/// The categories by id, in the order they were given.
	/// </summary>
	private readonly Dictionary<string, Category> _byId;

	/// <summary>
	/// The direct children by parent id, in the order the categories were given.
	/// </summary>
	private readonly Dictionary<string, List<Category>> _children = new(StringComparer.Ordinal);
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="CategoryTree"/> over a category list.
	/// </summary>
	/// <param name="categories">The categories; an id that appears twice keeps its first definition.</param>
	/// <exception cref="ArgumentNullException"><paramref name="categories"/> is <see langword="null"/>.</exception>
	public CategoryTree(IEnumerable<Category> categories)
	{
		ArgumentNullException.ThrowIfNull(categories);
		_byId = new Dictionary<string, Category>(StringComparer.Ordinal);
		foreach (var category in categories)
		{
			_byId.TryAdd(category.Id.Value, category);
		}

		foreach (var category in _byId.Values.Where(c => c.ParentId is not null && _byId.ContainsKey(c.ParentId.Value)))
		{
			var parentId = category.ParentId!.Value;
			if (!_children.TryGetValue(parentId, out var list))
			{
				_children[parentId] = list = [];
			}

			list.Add(category);
		}
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Gets the top-level categories: those without a parent, or whose parent does not exist.
	/// </summary>
	/// <returns>The roots, in the order the categories were given.</returns>
	public IReadOnlyList<Category> Roots() => [.. _byId.Values.Where(c => Parent(c.Id.Value) is null)];

	/// <summary>
	/// Determines whether a category exists.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns><see langword="true"/> when the tree holds it.</returns>
	public bool Contains(string id) => id is not null && _byId.ContainsKey(id);

	/// <summary>
	/// Gets a category's parent.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The parent, or <see langword="null"/> for a top-level or unknown category.</returns>
	public Category? Parent(string id) =>
		id is not null && _byId.TryGetValue(id, out var category) && category.ParentId is not null
			? _byId.GetValueOrDefault(category.ParentId.Value)
			: null;

	/// <summary>
	/// Gets a category's direct children.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The children, in the order the categories were given; empty for a leaf or an unknown id.</returns>
	public IReadOnlyList<Category> Children(string id) =>
		id is not null && _children.TryGetValue(id, out var list) ? list : [];

	/// <summary>
	/// Gets a category's ancestors.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The parent first and the root last; empty for a top-level or unknown category.</returns>
	public IReadOnlyList<Category> Ancestors(string id)
	{
		var result = new List<Category>();
		var seen = new HashSet<string>(StringComparer.Ordinal) { id };
		var parent = Parent(id);
		while (parent is not null && seen.Add(parent.Id.Value))
		{
			result.Add(parent);
			parent = Parent(parent.Id.Value);
		}

		return result;
	}

	/// <summary>
	/// Gets everything below a category, at any depth.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The descendants, level by level; empty for a leaf or an unknown id.</returns>
	public IReadOnlyList<Category> Descendants(string id)
	{
		var result = new List<Category>();
		var seen = new HashSet<string>(StringComparer.Ordinal) { id };
		var queue = new Queue<string>();
		queue.Enqueue(id);
		while (queue.TryDequeue(out var current))
		{
			foreach (var child in Children(current).Where(c => seen.Add(c.Id.Value)))
			{
				result.Add(child);
				queue.Enqueue(child.Id.Value);
			}
		}

		return result;
	}

	/// <summary>
	/// Gets a category's level: one for a top-level category.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The level; one for an unknown id.</returns>
	public int Depth(string id) => Ancestors(id).Count + 1;

	/// <summary>
	/// Gets the way from the top-level category down to a category.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The root first and the category itself last; empty for an unknown id.</returns>
	public IReadOnlyList<Category> Path(string id) =>
		id is not null && _byId.TryGetValue(id, out var category) ? [.. Ancestors(id).Reverse(), category] : [];

	/// <summary>
	/// Gets the top-level category a category belongs to.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The root (the category itself when it is top level), or <see langword="null"/> for an unknown id.</returns>
	public Category? Root(string id) => Path(id) is [var root, ..] ? root : null;

	/// <summary>
	/// Adds up amounts per category through the tree: the total of a category is its own amount plus the amounts of
	/// everything below it.
	/// </summary>
	/// <param name="byCategory">The amount of each category itself, by category id.</param>
	/// <returns>The rolled-up total of every category that has an amount of its own or below it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="byCategory"/> is <see langword="null"/>.</exception>
	public IReadOnlyDictionary<string, decimal> RollUp(IReadOnlyDictionary<string, decimal> byCategory)
	{
		ArgumentNullException.ThrowIfNull(byCategory);
		var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
		foreach (var (id, amount) in byCategory)
		{
			totals[id] = totals.GetValueOrDefault(id) + amount;
			foreach (var ancestorId in Ancestors(id).Select(a => a.Id.Value))
			{
				totals[ancestorId] = totals.GetValueOrDefault(ancestorId) + amount;
			}
		}

		return totals;
	}

	/// <summary>
	/// Checks that a category can be created or moved under a parent with the given type.
	/// </summary>
	/// <param name="id">The category being moved, or <see langword="null"/> for a new one.</param>
	/// <param name="parentId">The wanted parent, or <see langword="null"/> for a top-level category.</param>
	/// <param name="type">The category's type: for a sub-category it must be the type of the parent's root.</param>
	/// <returns>Success, or <see cref="CategoryErrors.BuiltInFixed"/> (a built-in category's parent or type changes),
	/// <see cref="CategoryErrors.ParentNotFound"/>, <see cref="CategoryErrors.ParentCycle"/>,
	/// <see cref="CategoryErrors.TooDeep"/> or <see cref="CategoryErrors.TypeFollowsRoot"/>.</returns>
	public Result Validate(string? id, string? parentId, CategoryType type)
	{
		if (id is not null && _byId.TryGetValue(id, out var existing)
			&& (IsBuiltIn(existing) && (existing.Type != type || !string.Equals(existing.ParentId?.Value, parentId, StringComparison.Ordinal))
				|| existing.Type != type && Descendants(id).Any(IsBuiltIn)))
		{
			return Result.Failure(CategoryErrors.BuiltInFixed);
		}

		if (parentId is null)
		{
			return Result.Success();
		}

		if (!_byId.TryGetValue(parentId, out var parent))
		{
			return Result.Failure(CategoryErrors.ParentNotFound);
		}

		if (id is not null && (string.Equals(id, parentId, StringComparison.Ordinal)
			|| Ancestors(parentId).Any(a => string.Equals(a.Id.Value, id, StringComparison.Ordinal))))
		{
			return Result.Failure(CategoryErrors.ParentCycle);
		}

		if (Depth(parent.Id.Value) + SubtreeHeight(id) > MaxDepth)
		{
			return Result.Failure(CategoryErrors.TooDeep);
		}

		return Root(parent.Id.Value)!.Type == type ? Result.Success() : Result.Failure(CategoryErrors.TypeFollowsRoot);
	}

	/// <summary>
	/// Checks that a category may be deleted.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>Success, or <see cref="CategoryErrors.BuiltInProtected"/> for a built-in category. An unknown id
	/// succeeds: there is nothing to protect.</returns>
	public Result ValidateDeletion(string id) =>
		id is not null && _byId.TryGetValue(id, out var category) && IsBuiltIn(category)
			? Result.Failure(CategoryErrors.BuiltInProtected)
			: Result.Success();
	#endregion

	#region Private Helpers
	/// <summary>
	/// Determines whether the analysis relies on a category: a built-in one, and <c>other</c> even when stored as a
	/// plain category.
	/// </summary>
	private static bool IsBuiltIn(Category category) => category.BuiltIn || category.Id == CategoryId.Other;

	/// <summary>
	/// Counts the levels of the subtree that starts at a category, itself included; one for a new category or a leaf.
	/// </summary>
	private int SubtreeHeight(string? id)
	{
		if (id is null)
		{
			return 1;
		}

		var height = 1;
		var levels = new Dictionary<string, int>(StringComparer.Ordinal) { [id] = 1 };
		var queue = new Queue<string>();
		queue.Enqueue(id);
		while (queue.TryDequeue(out var current))
		{
			foreach (var childId in Children(current).Select(c => c.Id.Value).Where(c => !levels.ContainsKey(c)))
			{
				levels[childId] = levels[current] + 1;
				height = Math.Max(height, levels[childId]);
				queue.Enqueue(childId);
			}
		}

		return height;
	}
	#endregion
}
