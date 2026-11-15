namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The log entry written when a provider account is refused: it names the provider's issuer and the account's
///              subject, which the owner copies into <c>Authentication:Owners</c>. Never the email address or any token.
/// </summary>
public static partial class OwnerRefusalLog
{
	#region Public Methods
	/// <summary>Records that a provider account is not on the allow-list and how the owner allows it.</summary>
	/// <param name="logger">The logger to write to.</param>
	/// <param name="issuer">The provider's issuer.</param>
	/// <param name="subject">The account's subject at that provider.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "A provider account was refused. If it is yours, add issuer {Issuer} and subject {Subject} to Authentication:Owners and restart the backend.")]
	public static partial void LogAccountRefused(this ILogger logger, string? issuer, string? subject);
	#endregion
}
