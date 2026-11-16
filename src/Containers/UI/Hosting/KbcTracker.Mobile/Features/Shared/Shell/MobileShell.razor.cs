namespace KbcTracker.Mobile.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The root component of the Android app. While nobody is signed in it shows the sign-in screen; once the owner
///              is, it renders the shared <see cref="KbcTrackerRouter"/> with a sign-out button as the header's account
///              controls. Unlike the web app's shell it passes no one-time notice, because the bank's callback never reaches
///              this app.
/// </summary>
public sealed partial class MobileShell : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the sign-in state of the app.</summary>
	[Inject]
	public required MobileSession Session { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the shell goes away, so the lookup of a stored session stops with it.</summary>
	private readonly CancellationTokenSource _lifetime = new();
	#endregion

	#region Lifecycle
	/// <inheritdoc />
	protected override async Task OnInitializedAsync()
	{
		Session.Changed += OnChanged;
		await Session.RestoreAsync(_lifetime.Token);
	}
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
	/// <summary>Redraws the shell when the sign-in state changed, possibly from another thread.</summary>
	private void OnChanged(object? sender, EventArgs e) => _ = InvokeAsync(StateHasChanged);
	#endregion
}
