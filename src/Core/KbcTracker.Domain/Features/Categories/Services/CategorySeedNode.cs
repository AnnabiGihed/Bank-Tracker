namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One node of the default category tree as written in code: where it sits, what it is called, its
///              COICOP 2018 code and the keywords that assign payments to it, split into the worldwide core and the
///              Belgian pack. A seed row that <see cref="CategoryDefaults"/> turns into a <see cref="Category"/>.
/// </summary>
/// <param name="Id">The category id; kept stable because it is stored in the user's decisions.</param>
/// <param name="ParentId">The parent's id, or <see langword="null"/> for a top-level category.</param>
/// <param name="Name">The friendly name.</param>
/// <param name="Coicop">The COICOP 2018 code, or <see langword="null"/> outside COICOP.</param>
/// <param name="CoreKeywords">The keywords loaded in every country.</param>
/// <param name="BelgiumKeywords">The keywords loaded only for Belgium.</param>
/// <param name="Type">The type of a top-level category; <see langword="null"/> on a child, which takes its root's.</param>
/// <param name="Color">The colour as <c>#rrggbb</c>; <see langword="null"/> on a child, which takes its root's.</param>
/// <param name="BuiltIn">Whether the analysis relies on the category.</param>
internal sealed record CategorySeedNode(string Id, string? ParentId, string Name, string? Coicop, string[] CoreKeywords,
	string[] BelgiumKeywords, CategoryType? Type = null, string? Color = null, bool BuiltIn = false);
