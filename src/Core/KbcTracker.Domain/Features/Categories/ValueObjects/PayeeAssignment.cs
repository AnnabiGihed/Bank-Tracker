namespace KbcTracker.Domain.Features.Categories.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The category of every payment of one payee series, as chosen or confirmed by the user.
/// </summary>
/// <param name="CategoryId">The category, or <see langword="null"/> to fall back to the automatic category.</param>
/// <param name="Confirmed">Whether the user confirmed the category (it then leaves the review queue).</param>
public sealed record PayeeAssignment(CategoryId? CategoryId, bool Confirmed);
