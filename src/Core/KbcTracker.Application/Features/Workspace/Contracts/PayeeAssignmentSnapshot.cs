using KbcTracker.Domain.Features.Categories.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Preserves the assignment returned to the UI for its transaction-only undo operation.
/// </summary>
/// <param name="CategoryId">The previous category, when any.</param>
/// <param name="Confirmed">Whether the previous assignment was confirmed.</param>
public sealed record PayeeAssignmentSnapshot(string? CategoryId, bool Confirmed)
{
	#region Public Methods
	/// <summary>Restores the previous assignment for a transaction-only category change.</summary>
	/// <returns>The assignment.</returns>
	public PayeeAssignment ToDomain() => new(string.IsNullOrWhiteSpace(CategoryId) ? null : new CategoryId(CategoryId), Confirmed);

	/// <summary>Copies the user's existing assignment.</summary>
	/// <param name="assignment">The stored assignment.</param>
	/// <returns>The undo snapshot.</returns>
	public static PayeeAssignmentSnapshot From(PayeeAssignment assignment) => new(assignment.CategoryId?.Value, assignment.Confirmed);
	#endregion
}
