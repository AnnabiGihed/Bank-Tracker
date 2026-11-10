namespace KbcTracker.Domain.Features.FixedPayments.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors creating or changing a fixed payment can fail with, coded
///              <c>RecurringPayment.Property.Reason</c>.
/// </summary>
public static class RecurringPaymentErrors
{
	#region Static Instances
	/// <summary>
	/// The fixed payment does not exist (any more).
	/// </summary>
	public static readonly Error NotFound = new("RecurringPayment.Id.NotFound", "This fixed payment does not exist.");

	/// <summary>
	/// The name is missing.
	/// </summary>
	public static readonly Error NameRequired = new("RecurringPayment.Name.Required", "The fixed payment needs a name.");

	/// <summary>
	/// The amount or cadence is not positive.
	/// </summary>
	public static readonly Error RuleInvalid = new("RecurringPayment.Rule.Invalid", "The amount and the rhythm must be positive.");

	/// <summary>
	/// It covers no payee: no series key and no shop word.
	/// </summary>
	public static readonly Error MatchRequired = new("RecurringPayment.Match.Required", "The fixed payment must cover at least one payee.");
	#endregion
}
