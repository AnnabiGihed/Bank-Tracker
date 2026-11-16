namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Keeps the refresh token in the platform's secure storage (the Android Keystore), the only place the app
///              stores a secret. Reading a token the keystore can no longer decrypt (after a restore to another device)
///              counts as no token, so the owner signs in again instead of the app failing.
/// </summary>
public sealed class SecureStorageRefreshTokenStore : IRefreshTokenStore
{
	#region Constants
	/// <summary>The key the token is stored under.</summary>
	private const string Key = "kbctracker.refresh-token";
	#endregion

	#region IRefreshTokenStore Implementation
	/// <inheritdoc />
	public async Task<string?> GetAsync(CancellationToken cancellationToken)
	{
		try
		{
			return await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or InvalidOperationException)
		{
			SecureStorage.Default.Remove(Key);
			return null;
		}
	}

	/// <inheritdoc />
	public Task SetAsync(string refreshToken, CancellationToken cancellationToken) => SecureStorage.Default.SetAsync(Key, refreshToken);

	/// <inheritdoc />
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		SecureStorage.Default.Remove(Key);
		return Task.CompletedTask;
	}
	#endregion
}
