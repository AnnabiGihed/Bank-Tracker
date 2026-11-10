namespace KbcTracker.Domain.Features.Shared.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors a user decision can fail with, coded <c>UserData.Property.Reason</c>.
/// </summary>
public static class UserDataErrors
{
	#region Static Instances
	/// <summary>
	/// The new payee name is blank.
	/// </summary>
	public static readonly Error PayeeNameRequired = new("UserData.PayeeName.Required", "The payee needs a name.");
	#endregion
}
