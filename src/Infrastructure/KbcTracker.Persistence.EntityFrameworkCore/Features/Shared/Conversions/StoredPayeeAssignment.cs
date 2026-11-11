namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A payee's category as stored in the <c>Payees</c> JSON column, in the same shape as the JSON data
///              folder (<c>CategoryId</c>, <c>Confirmed</c>), so the two read the same.
/// </summary>
/// <param name="CategoryId">The category id, or <see langword="null"/> for none.</param>
/// <param name="Confirmed">Whether the user confirmed it.</param>
internal sealed record StoredPayeeAssignment(string? CategoryId, bool Confirmed);
