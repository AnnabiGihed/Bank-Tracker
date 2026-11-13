namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One tab of the frame, with the number of items that wait on the owner.
/// </summary>
/// <param name="Id">The tab's id, also its address.</param>
/// <param name="Label">The tab's name.</param>
/// <param name="Badge">The number of open items, zero for none.</param>
public sealed record TabLink(string Id, string Label, int Badge);
