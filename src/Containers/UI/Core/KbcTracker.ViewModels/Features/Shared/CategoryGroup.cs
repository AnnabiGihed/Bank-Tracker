namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The options of a category picker that share a type, under the type's heading.
/// </summary>
/// <param name="Label">The heading: Spending, Income, Savings or Transfer.</param>
/// <param name="Type">The type the options share.</param>
/// <param name="Options">The categories in tree order (a parent before its children, siblings by name).</param>
public sealed record CategoryGroup(string Label, CategoryType Type, IReadOnlyList<CategoryOption> Options);
