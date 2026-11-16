namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Where a host keeps the refresh token between runs: the one long-lived secret of a sign-in. The Android app
///              backs it with the platform's secure storage; the access token is never stored.
/// </summary>
public interface IRefreshTokenStore
{
	#region Methods
	/// <summary>Reads the stored refresh token.</summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>The token, or <see langword="null"/> when nobody is signed in.</returns>
	Task<string?> GetAsync(CancellationToken cancellationToken);

	/// <summary>Replaces the stored refresh token.</summary>
	/// <param name="refreshToken">The new token.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the token is stored.</returns>
	Task SetAsync(string refreshToken, CancellationToken cancellationToken);

	/// <summary>Removes the stored refresh token.</summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when the token is gone.</returns>
	Task ClearAsync(CancellationToken cancellationToken);
	#endregion
}
