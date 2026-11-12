using System.Text.RegularExpressions;

namespace KbcTracker.Domain.Features.Categories.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A spending, income, savings or transfer category the user can edit: its name, colour, how it
///              counts in totals, and the keywords that assign payments to it automatically.
///
///              A lightweight aggregate root: its <see cref="Entity{TId}.Id"/> is stored in the user's decisions, so
///              it never changes once created; everything else changes only through <see cref="Update"/>.
/// </summary>
public sealed partial class Category : LightweightAggregateRoot<CategoryId>
{
	#region Constants
	/// <summary>
	/// The colour of a category whose colour was never chosen.
	/// </summary>
	public const string DefaultColor = "#828282";
	#endregion

	#region Fields
	/// <summary>
	/// The keywords, in the order the user typed them.
	/// </summary>
	private readonly List<string> _keywords = [];
	#endregion

	#region Properties
	/// <summary>
	/// Gets the name shown to the user.
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets how the category counts in the totals.
	/// </summary>
	public CategoryType Type { get; private set; }

	/// <summary>
	/// Gets the colour used in charts, as <c>#rrggbb</c>.
	/// </summary>
	public string Color { get; private set; }

	/// <summary>
	/// Gets the words looked for at the start of a word in the payee or shop name (and in the description for
	/// non-card payments). When several categories match, the longest keyword wins.
	/// </summary>
	public IReadOnlyList<string> Keywords => _keywords.AsReadOnly();

	/// <summary>
	/// Gets a value indicating whether the analysis relies on this category (income, loan, own transfers…).
	/// </summary>
	public bool BuiltIn { get; private set; }

	/// <summary>
	/// Gets the parent category, or <see langword="null"/> for a top-level category. The tree rules (the parent
	/// exists, no cycle, at most four levels, the type of the root) are checked by <see cref="CategoryTree"/>.
	/// </summary>
	public CategoryId? ParentId { get; private set; }

	/// <summary>
	/// Gets the COICOP 2018 code the category corresponds to (<c>07.2</c>), or <see langword="null"/> when it has
	/// none. Informational only: nothing in the analysis reads it.
	/// </summary>
	public string? Coicop { get; private set; }
	#endregion

	#region Constructors
	/// <summary>
	/// Constructor used by EF Core to materialise a stored category; the persistence layer then sets its values. Domain
	/// code creates instances only through the factory methods. It is <see langword="internal"/> rather than private
	/// because the analyzers report an unused private constructor (S1144) without seeing EF Core's reflection.
	/// </summary>
	internal Category()
	{
		Name = "";
		Color = DefaultColor;
	}

	/// <summary>
	/// Initialises a new <see cref="Category"/> from already valid values.
	/// </summary>
	private Category(CategoryId id, string name, CategoryType type, string color, IEnumerable<string> keywords, bool builtIn,
		CategoryId? parentId, string? coicop)
		: base(id)
	{
		ParentId = parentId;
		Coicop = coicop;
		Name = name;
		Type = type;
		Color = color;
		BuiltIn = builtIn;
		_keywords.AddRange(keywords);
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates a category.
	/// </summary>
	/// <param name="id">The category's identifier.</param>
	/// <param name="name">The name; required, trimmed.</param>
	/// <param name="type">How it counts in the totals.</param>
	/// <param name="color">The colour, as <c>#rrggbb</c>.</param>
	/// <param name="keywords">The keywords; blank ones are dropped, the others kept exactly as given.</param>
	/// <param name="builtIn">Whether the analysis relies on it.</param>
	/// <param name="parentId">The parent category, or <see langword="null"/> for a top-level category.</param>
	/// <param name="coicop">The COICOP 2018 code, or <see langword="null"/>.</param>
	/// <returns>The category, or <see cref="CategoryErrors.NameRequired"/> or <see cref="CategoryErrors.ColorInvalid"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="keywords"/> is <see langword="null"/>.</exception>
	public static Result<Category> Create(CategoryId id, string name, CategoryType type, string color, IEnumerable<string> keywords, bool builtIn,
		CategoryId? parentId = null, string? coicop = null)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(keywords);
		var problem = Validate(name, color);
		return problem is null
			? new Category(id, name.Trim(), type, color, CleanKeywords(keywords), builtIn, parentId, coicop)
			: Result.Failure<Category>(problem);
	}

	/// <summary>
	/// Rebuilds a stored category without the creation checks: stored categories are kept as they are.
	/// </summary>
	/// <param name="id">The category's identifier.</param>
	/// <param name="name">The stored name.</param>
	/// <param name="type">The stored type.</param>
	/// <param name="color">The stored colour.</param>
	/// <param name="keywords">The stored keywords, in order.</param>
	/// <param name="builtIn">Whether it is built in.</param>
	/// <param name="parentId">The stored parent, or <see langword="null"/>.</param>
	/// <param name="coicop">The stored COICOP code, or <see langword="null"/>.</param>
	/// <returns>The category.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static Category Rehydrate(CategoryId id, string name, CategoryType type, string color, IEnumerable<string> keywords, bool builtIn,
		CategoryId? parentId = null, string? coicop = null)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(color);
		ArgumentNullException.ThrowIfNull(keywords);
		return new Category(id, name, type, color, keywords, builtIn, parentId, coicop);
	}
	#endregion

	#region Domain Behaviours
	/// <summary>
	/// Changes the category as the user edited it.
	/// </summary>
	/// <param name="name">The new name; required, trimmed.</param>
	/// <param name="type">How it counts in the totals.</param>
	/// <param name="color">The new colour, as <c>#rrggbb</c>.</param>
	/// <param name="keywords">The new keywords; blank ones are dropped, the others kept exactly as given.</param>
	/// <param name="parentId">The new parent, or <see langword="null"/> to make it a top-level category.</param>
	/// <returns>Success, or <see cref="CategoryErrors.NameRequired"/> or <see cref="CategoryErrors.ColorInvalid"/>
	/// with the category unchanged.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="keywords"/> is <see langword="null"/>.</exception>
	public Result Update(string name, CategoryType type, string color, IEnumerable<string> keywords, CategoryId? parentId)
	{
		ArgumentNullException.ThrowIfNull(keywords);
		var problem = Validate(name, color);
		if (problem is not null)
		{
			return Result.Failure(problem);
		}

		// Copied first: callers may pass this category's own keyword list, which the Clear below would empty.
		var cleaned = CleanKeywords(keywords).ToList();
		Name = name.Trim();
		Type = type;
		Color = color;
		ParentId = parentId;
		_keywords.Clear();
		_keywords.AddRange(cleaned);
		return Result.Success();
	}

	/// <summary>
	/// Sets the COICOP 2018 code, which the upgrade of an existing database gives to the categories it attaches to the
	/// default tree.
	/// </summary>
	/// <param name="coicop">The code, or <see langword="null"/> for none.</param>
	public void SetCoicop(string? coicop) => Coicop = coicop;
	#endregion

	#region Private Helpers
	/// <summary>
	/// Returns the first broken rule of a name and colour, or <see langword="null"/> when both are valid.
	/// </summary>
	private static Error? Validate(string name, string color)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return CategoryErrors.NameRequired;
		}

		return color is not null && HexColor().IsMatch(color) ? null : CategoryErrors.ColorInvalid;
	}

	/// <summary>
	/// Drops blank keywords and keeps the others exactly as given, in order: a trailing space is meaningful
	/// (<c>"AD "</c> matches the word "AD" only, not "ADIDAS").
	/// </summary>
	private static IEnumerable<string> CleanKeywords(IEnumerable<string> keywords) =>
		keywords.Where(k => !string.IsNullOrWhiteSpace(k));

	/// <summary>
	/// Matches a <c>#rrggbb</c> colour.
	/// </summary>
	[GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.None, 1000)]
	private static partial Regex HexColor();
	#endregion
}
