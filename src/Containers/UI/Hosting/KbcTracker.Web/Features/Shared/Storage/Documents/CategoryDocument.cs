using System.Text.Json.Serialization;

namespace KbcTracker.Web.Features.Shared.Storage.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One category as stored in <c>categories.json</c>, in the file's existing shape.
/// </summary>
internal sealed record CategoryDocument
{
	#region Properties
	/// <summary>
	/// Gets the category id.
	/// </summary>
	public string Id { get; init; } = "";

	/// <summary>
	/// Gets the name.
	/// </summary>
	public string Name { get; init; } = "";

	/// <summary>
	/// Gets how its payments count in the totals.
	/// </summary>
	public CategoryType Type { get; init; } = CategoryType.Expense;

	/// <summary>
	/// Gets the colour, as <c>#rrggbb</c>.
	/// </summary>
	public string Color { get; init; } = Category.DefaultColor;

	/// <summary>
	/// Gets the keywords looked for in payee and shop names.
	/// </summary>
	public IReadOnlyList<string> Keywords { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether the analysis relies on it.
	/// </summary>
	public bool BuiltIn { get; init; }

	/// <summary>
	/// Gets a value indicating whether the category has an id. Older versions could save one without (a name with
	/// no letter or digit); nothing can refer to it, so the store leaves it out.
	/// </summary>
	[JsonIgnore]
	public bool HasId => !string.IsNullOrWhiteSpace(Id);
	#endregion

	#region Public Methods
	/// <summary>
	/// Stores a category.
	/// </summary>
	/// <param name="category">The category.</param>
	/// <returns>The document.</returns>
	public static CategoryDocument From(Category category) => new()
	{
		Id = category.Id.Value,
		Name = category.Name,
		Type = category.Type,
		Color = category.Color,
		Keywords = [.. category.Keywords],
		BuiltIn = category.BuiltIn,
	};

	/// <summary>
	/// Rebuilds the stored category as it is; missing text reads as empty and a missing colour as the default.
	/// The store skips categories with a blank id (see <see cref="HasId"/>).
	/// </summary>
	/// <returns>The category.</returns>
	public Category ToDomain() => Category.Rehydrate(new CategoryId(Id), Name ?? "", Type, Color ?? Category.DefaultColor, Keywords ?? [], BuiltIn);
	#endregion
}
