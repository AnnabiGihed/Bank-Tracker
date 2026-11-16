namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The screen shown while nobody is signed in: one button per provider, which opens the system browser, and the
///              reason the last attempt or session failed.
/// </summary>
public sealed partial class SignInScreen : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the sign-in state of the app.</summary>
	[Inject]
	public required MobileSession Session { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the screen goes away, so a token request stops with it.</summary>
	private readonly CancellationTokenSource _lifetime = new();
	#endregion

	#region Lifecycle
	/// <inheritdoc />
	protected override void OnInitialized() => Session.Changed += OnChanged;
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		Session.Changed -= OnChanged;
		_lifetime.Cancel();
		_lifetime.Dispose();
	}
	#endregion

	#region Event Handlers
	/// <summary>Starts the sign-in with one provider.</summary>
	/// <param name="provider">The provider on the button.</param>
	/// <returns>A task that completes when the attempt ended.</returns>
	private Task SignInAsync(string provider) => Session.SignInAsync(provider, _lifetime.Token);

	/// <summary>Redraws the screen when the state changed, possibly from another thread.</summary>
	private void OnChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);
	#endregion
}
