using KbcTracker.Domain.Features.Categories.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers a category definition independently of persistence constructors.
/// </summary>
/// <param name="Id">The stable category identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Type">The accounting treatment.</param>
/// <param name="Color">The display colour.</param>
/// <param name="Keywords">The automatic matching terms.</param>
/// <param name="BuiltIn">Whether the definition was supplied with the tracker.</param>
/// <param name="ParentId">The parent category, or <see langword="null"/> for a top-level category.</param>
/// <param name="Coicop">The COICOP 2018 code, or <see langword="null"/>.</param>
public sealed record CategorySnapshot(string Id, string Name, CategoryType Type, string Color, IReadOnlyList<string> Keywords, bool BuiltIn,
	string? ParentId = null, string? Coicop = null)
{
	#region Public Methods
	/// <summary>Rebuilds the definition for rendering.</summary>
	/// <returns>The category.</returns>
	public Category ToDomain() => Category.Rehydrate(new CategoryId(Id), Name, Type, Color, Keywords, BuiltIn,
		ParentId is null ? null : new CategoryId(ParentId), Coicop);

	/// <summary>Copies a definition into its transport representation.</summary>
	/// <param name="category">The centrally stored definition.</param>
	/// <returns>The snapshot.</returns>
	public static CategorySnapshot From(Category category) => new(category.Id.Value, category.Name, category.Type, category.Color, [.. category.Keywords], category.BuiltIn,
		category.ParentId?.Value, category.Coicop);
	#endregion
}
