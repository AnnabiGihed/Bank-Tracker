namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One slice of the doughnut of where the month's money went: a category with what it and everything below
///              it spent, or the part of a category that no sub-category claims.
/// </summary>
/// <param name="Id">The category's id.</param>
/// <param name="Name">The category's name; "– not specified" is added for the part no sub-category claims.</param>
/// <param name="Color">The slice's colour, as <c>#rrggbb</c>.</param>
/// <param name="Value">The amount, rounded to the cent.</param>
/// <param name="CanDrill">Whether the slice has sub-categories with spending to drill into; otherwise a click opens its payments.</param>
public sealed record CategorySlice(string Id, string Name, string Color, decimal Value, bool CanDrill);
