namespace KbcTracker.Infrastructure.Features.Legacy.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A payee's category as stored in <c>userdata.json</c> and exchanged with the pages (the "only this
///              one" undo sends it back), in its existing shape.
/// </summary>
/// <param name="CategoryId">The category id, if any.</param>
/// <param name="Confirmed">Whether the user confirmed it.</param>
public sealed record PayeeAssignmentDocument(string? CategoryId, bool Confirmed)
{
	#region Public Methods
	/// <summary>
	/// Stores a payee assignment.
	/// </summary>
	/// <param name="assignment">The assignment.</param>
	/// <returns>The document.</returns>
	public static PayeeAssignmentDocument From(PayeeAssignment assignment) => new(assignment.CategoryId?.Value, assignment.Confirmed);

	/// <summary>
	/// Rebuilds the stored assignment; a blank category keeps its old meaning, "Other".
	/// </summary>
	/// <returns>The assignment.</returns>
	public PayeeAssignment ToDomain() => new(LegacyValues.Category(CategoryId), Confirmed);
	#endregion
}
