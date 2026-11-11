namespace KbcTracker.Application.Features.Categories.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Returns a centrally managed category definition to clients.
/// </summary>
/// <param name="Id">The stable category identifier.</param>
/// <param name="Name">The category label.</param>
/// <param name="Type">The category's totals classification.</param>
/// <param name="Color">The display colour.</param>
/// <param name="Keywords">The classification keywords.</param>
public sealed record CategorySummary(string Id, string Name, string Type, string Color, IReadOnlyList<string> Keywords);
