namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Returns the identifier or undo assignment needed by the existing UI after a successful decision.
/// </summary>
/// <param name="Id">The saved category identifier, when relevant.</param>
/// <param name="PreviousAssignment">The payee assignment before the change, when relevant.</param>
public sealed record DecisionResponse(string? Id = null, PayeeAssignmentSnapshot? PreviousAssignment = null);
