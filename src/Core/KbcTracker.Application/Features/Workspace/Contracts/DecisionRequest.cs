namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries an existing UI decision to the central backend for validation and one atomic commit.
/// </summary>
/// <param name="Action">The existing action name.</param>
/// <param name="Key">A suggestion signature or payee series key.</param>
/// <param name="Id">A fixed payment, transaction or category id.</param>
/// <param name="Keys">The payee keys to confirm together.</param>
/// <param name="Name">The requested display name.</param>
/// <param name="CategoryId">The chosen category.</param>
/// <param name="Type">The category treatment name.</param>
/// <param name="Color">The category colour.</param>
/// <param name="Keywords">Comma or newline separated category matching terms.</param>
/// <param name="Amount">The new fixed-payment amount.</param>
/// <param name="Cadence">The recurrence interval in days.</param>
/// <param name="Restore">The assignment to restore for a transaction-only change.</param>
/// <param name="ParentId">For <c>saveCategory</c>: the parent category. Omitted, a new category is top level and an
/// existing one keeps its parent; an empty value makes an existing category top level.</param>
public sealed record DecisionRequest(string Action, string? Key = null, string? Id = null, string[]? Keys = null, string? Name = null,
	string? CategoryId = null, string? Type = null, string? Color = null, string? Keywords = null,
	decimal? Amount = null, int? Cadence = null, PayeeAssignmentSnapshot? Restore = null,
	string? ParentId = null);
