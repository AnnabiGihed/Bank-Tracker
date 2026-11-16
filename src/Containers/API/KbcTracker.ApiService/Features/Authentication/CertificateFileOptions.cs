namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Points at one persisted PKCS#12 certificate file that signs or encrypts the API's tokens.
/// </summary>
public sealed class CertificateFileOptions
{
	#region Properties
	/// <summary>Gets or sets the path of the certificate file, kept outside the repository; blank means none.</summary>
	public string Path { get; set; } = string.Empty;

	/// <summary>Gets or sets the file's password, if it has one; keep it in user secrets or server settings.</summary>
	public string Password { get; set; } = string.Empty;
	#endregion
}
