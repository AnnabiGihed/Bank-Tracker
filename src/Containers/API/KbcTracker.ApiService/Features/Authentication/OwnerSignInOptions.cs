namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The <c>Authentication</c> configuration section (ADR-0009): who the owner is, the providers they sign in
///              with, the registered clients and the certificates that keep the tokens valid across restarts. Secrets
///              come from user secrets or server settings, never the repository.
/// </summary>
public sealed class OwnerSignInOptions
{
	#region Constants
	/// <summary>The configuration section.</summary>
	public const string SectionName = "Authentication";

	/// <summary>The access token lifetime when none is configured.</summary>
	public const int DefaultAccessTokenSeconds = 600;
	#endregion

	#region Properties
	/// <summary>Gets the provider accounts that may use the backend; anyone else is refused at sign-in.</summary>
	public ICollection<AllowedOwner> Owners { get; } = [];

	/// <summary>Gets the enabled providers by name (for example <c>Google</c>); a provider is on when its section exists.</summary>
	public IDictionary<string, ProviderOptions> Providers { get; } = new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase);

	/// <summary>Gets the registered clients' settings.</summary>
	public ClientsOptions Clients { get; } = new();

	/// <summary>Gets the certificate that signs tokens; required outside Development.</summary>
	public CertificateFileOptions Signing { get; } = new();

	/// <summary>Gets the certificate that encrypts tokens; required outside Development.</summary>
	public CertificateFileOptions Encryption { get; } = new();

	/// <summary>Gets or sets the access token lifetime in seconds.</summary>
	public int AccessTokenSeconds { get; set; } = DefaultAccessTokenSeconds;
	#endregion
}
