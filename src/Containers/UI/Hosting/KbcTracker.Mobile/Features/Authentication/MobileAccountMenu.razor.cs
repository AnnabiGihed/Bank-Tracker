namespace KbcTracker.Mobile.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Android app adds to the shared header: a sign-out button. The shell hands it to the layout as the
///              cascading <c>AccountMenu</c> fragment, as the web app does with its own account controls.
/// </summary>
public sealed partial class MobileAccountMenu : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the sign-in state of the app.</summary>
	[Inject]
	public required MobileSession Session { get; set; }
	#endregion

	#region Lifecycle
	/// <inheritdoc />
	protected override void OnInitialized() => Session.Changed += OnChanged;
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => Session.Changed -= OnChanged;
	#endregion

	#region Event Handlers
	/// <summary>Signs the owner out; the shell then shows the sign-in screen.</summary>
	/// <returns>A task that completes when the session is gone.</returns>
	private Task SignOutAsync() => Session.SignOutAsync(CancellationToken.None);

	/// <summary>Redraws the button when the state changed, possibly from another thread.</summary>
	private void OnChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);
	#endregion
}
