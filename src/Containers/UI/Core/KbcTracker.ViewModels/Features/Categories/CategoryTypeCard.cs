namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The card of one category type on the Categories tab: its heading, the total of its top-level
///              categories and its categories in tree order.
/// </summary>
/// <param name="Type">The type the card lists.</param>
/// <param name="Title">The heading.</param>
/// <param name="Total">The month's total of the top-level categories.</param>
/// <param name="TotalText">The total, formatted.</param>
/// <param name="Rows">The categories in tree order, siblings by their total in the month, largest first.</param>
public sealed record CategoryTypeCard(CategoryType Type, string Title, decimal Total, string TotalText, IReadOnlyList<CategoryRow> Rows);
