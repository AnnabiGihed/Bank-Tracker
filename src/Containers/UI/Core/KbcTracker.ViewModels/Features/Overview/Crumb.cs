namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One step of the path above the doughnut: "All", then each category the owner drilled into.
/// </summary>
/// <param name="Id">The category's id; empty for "All".</param>
/// <param name="Label">The category's name, or "All".</param>
/// <param name="IsCurrent">Whether it is the level shown now, which is not a link.</param>
public sealed record Crumb(string Id, string Label, bool IsCurrent);
