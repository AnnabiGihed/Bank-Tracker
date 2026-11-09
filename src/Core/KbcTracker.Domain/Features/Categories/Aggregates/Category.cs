namespace KbcTracker.Domain.Features.Categories.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A spending, income, savings or transfer category the user can edit: its name, colour, how it
///              counts in totals, and the keywords that assign payments to it automatically.
///
///              Its <see cref="Id"/> is stored in the user's decisions, so it never changes once created.
/// </summary>
public sealed class Category
{
	#region Properties
	/// <summary>
	/// Gets or sets the stable id (see <see cref="CategoryDefaults.Slug"/>); <c>other</c> is the fallback category.
	/// </summary>
	public string Id { get; set; } = "";

	/// <summary>
	/// Gets or sets the name shown to the user.
	/// </summary>
	public string Name { get; set; } = "";

	/// <summary>
	/// Gets or sets how the category counts in the totals.
	/// </summary>
	public CategoryType Type { get; set; } = CategoryType.Expense;

	/// <summary>
	/// Gets or sets the colour used in charts, as <c>#rrggbb</c>.
	/// </summary>
	public string Color { get; set; } = "#828282";

	/// <summary>
	/// Gets or sets the words looked for at the start of a word in the payee or shop name (and in the description
	/// for non-card payments). When several categories match, the longest keyword wins.
	/// </summary>
	public IList<string> Keywords { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the analysis relies on this category (income, loan, own transfers…).
	/// </summary>
	public bool BuiltIn { get; set; }
	#endregion
}
