namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One section of the Income tab, such as the earnings of the month: the Overview figure it explains, its
///              total (exactly that figure) and its groups of payments.
/// </summary>
/// <param name="Id">The section's id, the <c>show</c> query value of its figure.</param>
/// <param name="Title">The section's name, as the Overview names the figure.</param>
/// <param name="Total">What its payments add up to.</param>
/// <param name="TotalText">The total, formatted.</param>
/// <param name="Groups">Its groups of payments; empty when the month has none of this kind.</param>
/// <param name="EmptyText">What the section says when it has no payment.</param>
public sealed record IncomeSection(string Id, string Title, decimal Total, string TotalText, IReadOnlyList<IncomeGroup> Groups, string EmptyText);
