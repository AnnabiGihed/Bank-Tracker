namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The body of <c>POST /api</c>: one user action from the pages' JavaScript, with the fields that action
///              needs. Which fields are used depends on <see cref="Action"/>; the others stay <see langword="null"/>.
/// </summary>
/// <param name="Action">What to do: accept, dismiss, mark, updateFixed, removeFixed, payeeCategory, txnOnly, confirm,
/// rename, saveCategory or deleteCategory.</param>
/// <param name="Key">A suggestion signature or a payee series key.</param>
/// <param name="Id">A fixed payment, transaction or category id.</param>
/// <param name="Keys">Several payee series keys, for "confirm all shown".</param>
/// <param name="Name">A new name for a fixed payment, payee or category.</param>
/// <param name="CategoryId">The category to assign.</param>
/// <param name="Type">A category type name (see <see cref="CategoryType"/>).</param>
/// <param name="Color">A category colour, as <c>#rrggbb</c>.</param>
/// <param name="Keywords">Category keywords, separated by commas or new lines.</param>
/// <param name="Amount">A fixed payment's new usual amount.</param>
/// <param name="Cadence">A fixed payment's new cadence in days.</param>
/// <param name="Restore">The payee's previous assignment, to undo it when the user picks "only this one".</param>
internal sealed record ApiRequest(string Action, string? Key = null, string? Id = null, string[]? Keys = null, string? Name = null, string? CategoryId = null,
	string? Type = null, string? Color = null, string? Keywords = null, decimal? Amount = null, int? Cadence = null, PayeeAssignmentDocument? Restore = null);
