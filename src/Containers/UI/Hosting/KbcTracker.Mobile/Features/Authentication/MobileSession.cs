using System.Buffers.Text;
using System.Security.Cryptography;

namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The owner's sign-in state in the app, and what changes it: restoring a stored session at startup, signing
///              in through the system browser (<see cref="WebAuthenticator"/>, code flow with PKCE) and signing out. The token
///              rules live in <see cref="PublicClientTokenService"/>; this class only joins them to the platform and tells the
///              shell, through <see cref="Changed"/>, when to show the sign-in screen or the tabs.
/// </summary>
/// <param name="tokens">The token logic.</param>
/// <param name="options">The providers to offer.</param>
/// <param name="client">Where the browser returns to, which <see cref="WebAuthenticator"/> must wait for.</param>
public sealed class MobileSession(PublicClientTokenService tokens, MobileSignInOptions options, PublicClientOptions client) : IDisposable
{
	#region Constants
	/// <summary>The number of random bytes of the state value.</summary>
	private const int StateBytes = 32;
	#endregion

	#region Fields
	/// <summary>Whether a sign-in or sign-out is running, so a second tap does not start another.</summary>
	private bool _busy;

	/// <summary>Whether this session listens to the token service.</summary>
	private bool _subscribed;
	#endregion

	#region Events
	/// <summary>Raised when <see cref="State"/>, <see cref="Message"/> or <see cref="IsBusy"/> changed.</summary>
	public event EventHandler? Changed;
	#endregion

	#region Properties
	/// <summary>Gets where the owner is in signing in.</summary>
	public SessionState State { get; private set; } = SessionState.Starting;

	/// <summary>Gets the last problem to tell the owner, or <see langword="null"/> for none.</summary>
	public string? Message { get; private set; }

	/// <summary>Gets a value indicating whether a sign-in or sign-out is running.</summary>
	public bool IsBusy => _busy;

	/// <summary>Gets the providers the sign-in screen offers.</summary>
	public IReadOnlyList<string> Providers => options.Providers;
	#endregion

	#region Public Methods
	/// <summary>Looks for a stored session and decides which screen to show.</summary>
	/// <param name="cancellationToken">Cancels the lookup.</param>
	/// <returns>A task that completes when the state is known.</returns>
	public async Task RestoreAsync(CancellationToken cancellationToken)
	{
		Subscribe();
		try
		{
			State = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false) is null ? SessionState.SignedOut : SessionState.SignedIn;
			Message = null;
		}
		catch (HttpRequestException)
		{
			State = SessionState.SignedOut;
			Message = "The backend cannot be reached. Start it, run adb reverse tcp:5080 tcp:5080 and try again.";
		}

		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Signs the owner in at a provider through the system browser.</summary>
	/// <param name="provider">The provider to sign in with.</param>
	/// <param name="cancellationToken">Cancels the token request.</param>
	/// <returns>A task that completes when the owner is signed in or the attempt failed; the reason is in <see cref="Message"/>.</returns>
	public async Task SignInAsync(string provider, CancellationToken cancellationToken)
	{
		if (_busy)
		{
			return;
		}

		SetBusy(true);
		try
		{
			var proof = PkceChallenge.Create();
			var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(StateBytes));
			var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
			{
				Url = tokens.BuildAuthorizeUri(proof, state, provider),
				CallbackUrl = client.RedirectUri,
			}, cancellationToken).ConfigureAwait(false);
			var code = AuthorizationCallback.ReadCode(new Dictionary<string, string>(result.Properties, StringComparer.Ordinal), state);
			await tokens.SignInAsync(code, proof.Verifier, cancellationToken).ConfigureAwait(false);
			State = SessionState.SignedIn;
			Message = null;
		}
		catch (TaskCanceledException)
		{
			Message = "Sign-in was cancelled.";
		}
		catch (HttpRequestException)
		{
			Message = "The backend did not accept the sign-in or cannot be reached. Check that it runs and try again.";
		}
		catch (InvalidOperationException exception)
		{
			Message = exception.Message;
		}
		finally
		{
			SetBusy(false);
		}
	}

	/// <summary>Signs the owner out: revokes the refresh token and forgets every token.</summary>
	/// <param name="cancellationToken">Cancels the revocation.</param>
	/// <returns>A task that completes when the sign-in screen can be shown.</returns>
	public async Task SignOutAsync(CancellationToken cancellationToken)
	{
		SetBusy(true);
		try
		{
			var revoked = await tokens.SignOutAsync(cancellationToken).ConfigureAwait(false);
			State = SessionState.SignedOut;
			Message = revoked ? null : "Signed out here, but the backend could not be asked to revoke the session.";
		}
		finally
		{
			SetBusy(false);
		}
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		if (_subscribed)
		{
			tokens.SessionEnded -= OnSessionEnded;
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Starts listening for a session the API ended.</summary>
	private void Subscribe()
	{
		if (!_subscribed)
		{
			tokens.SessionEnded += OnSessionEnded;
			_subscribed = true;
		}
	}

	/// <summary>Shows the sign-in screen when the API refused the refresh token.</summary>
	private void OnSessionEnded(object? sender, EventArgs e)
	{
		State = SessionState.SignedOut;
		Message = "Your session ended. Sign in again.";
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Sets the busy flag and tells the listeners.</summary>
	private void SetBusy(bool busy)
	{
		_busy = busy;
		Changed?.Invoke(this, EventArgs.Empty);
	}
	#endregion
}
