namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Signals that the bank provider cannot be called because the backend lacks its application id or private
///              key, so the use cases can tell the owner what to set up instead of "start again later", which would never
///              help.
/// </summary>
public sealed class BankAccessNotConfiguredException : InvalidOperationException
{
	#region Constructors
	/// <summary>Initialises a new <see cref="BankAccessNotConfiguredException"/> with a default message.</summary>
	public BankAccessNotConfiguredException()
		: base("Bank access is not configured.")
	{
	}

	/// <summary>Initialises a new <see cref="BankAccessNotConfiguredException"/>.</summary>
	/// <param name="message">Which setting is missing; never a secret value.</param>
	public BankAccessNotConfiguredException(string message)
		: base(message)
	{
	}

	/// <summary>Initialises a new <see cref="BankAccessNotConfiguredException"/> wrapping the failure it explains.</summary>
	/// <param name="message">Which setting is missing; never a secret value.</param>
	/// <param name="innerException">The failure caused by the missing setting.</param>
	public BankAccessNotConfiguredException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
	#endregion
}
