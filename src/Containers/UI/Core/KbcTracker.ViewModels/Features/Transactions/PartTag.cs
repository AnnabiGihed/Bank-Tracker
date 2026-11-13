namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One part of a split payment as the Transactions tab shows it: a coloured tag with the part's label or
///              category and its amount.
/// </summary>
/// <param name="Text">The label, or the category name when the part has none, and the amount.</param>
/// <param name="CategoryColor">The category's colour.</param>
/// <param name="Path">The category's place in the tree, such as <c>Transport › Train</c>.</param>
public sealed record PartTag(string Text, string CategoryColor, string Path);
