using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KbcTracker.E2E.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Creates throw-away PKCS#12 key files once per test run, so every test host signs and encrypts its tokens
///              with configured certificates, as a host outside Development must, instead of the user's certificate store.
/// </summary>
internal static class TestCertificates
{
	#region Fields
	/// <summary>The files, created on first use.</summary>
	private static readonly Lazy<(string Signing, string Encryption)> Files = new(Create);
	#endregion

	#region Properties
	/// <summary>Gets the path of the signing certificate file.</summary>
	public static string SigningPath => Files.Value.Signing;

	/// <summary>Gets the path of the encryption certificate file.</summary>
	public static string EncryptionPath => Files.Value.Encryption;
	#endregion

	#region Private Helpers
	/// <summary>Writes both files into a new temporary folder.</summary>
	private static (string Signing, string Encryption) Create()
	{
		var folder = Path.Combine(Path.GetTempPath(), "kbctracker-test-keys-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(folder);
		return (Write(Path.Combine(folder, "signing.pfx"), X509KeyUsageFlags.DigitalSignature),
			Write(Path.Combine(folder, "encryption.pfx"), X509KeyUsageFlags.KeyEncipherment));
	}

	/// <summary>Writes one self-signed RSA certificate with the given key usage.</summary>
	private static string Write(string path, X509KeyUsageFlags usage)
	{
		using var key = RSA.Create(2048);
		var request = new CertificateRequest("CN=KbcTracker test keys", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));
		using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
		File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx));
		return path;
	}
	#endregion
}
