namespace KbcTracker.Domain.Features.BankConnection.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors connecting to the bank can fail with, coded <c>Session.Property.Reason</c>.
/// </summary>
public static class SessionErrors
{
	#region Static Instances
	/// <summary>
	/// The consent would end before it starts.
	/// </summary>
	public static readonly Error ValidityInvalid = new("Session.ValidUntil.Invalid", "The bank access must end after it starts.");
	#endregion
}
