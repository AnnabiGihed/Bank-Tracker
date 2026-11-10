namespace KbcTracker.Web.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Holds the category keyword lists from <c>appsettings.json</c>, used only to create
///              <c>categories.json</c> the first time. In step 3 of ADR-0003 the seed moves into the database and this
///              class goes away.
/// </summary>
public static class CategorySeed
{
	#region Properties
	/// <summary>
	/// Gets or sets the category names with their keywords, as read from the <c>Categories</c> configuration section.
	/// </summary>
	public static IReadOnlyDictionary<string, string[]> Keywords { get; set; } = new Dictionary<string, string[]>(StringComparer.Ordinal);
	#endregion
}
