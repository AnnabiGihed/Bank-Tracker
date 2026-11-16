namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries the web client's secret to the services that need it, so none reads configuration on its own.
///              Its text form hides the secret, so a log line or an exception never prints it.
/// </summary>
/// <param name="Value">The client secret.</param>
public sealed record WebClientSecret(string Value)
{
	#region Public Methods
	/// <inheritdoc />
	public override string ToString() => nameof(WebClientSecret);
	#endregion
}
