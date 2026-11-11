using Pivot.Framework.Application.Abstractions.Messaging.Queries;

namespace KbcTracker.Application.Features.Categories.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Requests the centrally stored category definitions.
/// </summary>
public sealed record ListCategoriesQuery : IQuery<IReadOnlyList<CategorySummary>>;
