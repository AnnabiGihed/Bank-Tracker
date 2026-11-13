using System.Web;

namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The frame of every page: the Radzen host components and theme, the header with the sync status and the
///              month selector, the tab bar with its badges, the sync banner, the backend status and an error boundary
///              that catches anything a page does not handle itself. It reads the month from <c>?m=yyyy-MM</c>, hands it
///              to the <see cref="WorkspaceStore"/>, and shows a one-time notice a host passes in.
/// </summary>
public sealed partial class MainLayout : LayoutComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the store that holds the workspace.</summary>
	[Inject]
	public required WorkspaceStore Store { get; set; }

	/// <summary>Gets or sets the navigation manager.</summary>
	[Inject]
	public required NavigationManager Navigation { get; set; }

	/// <summary>Gets or sets the service that shows notifications.</summary>
	[Inject]
	public required NotificationService Notifications { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets a one-time failure message a host passes in, for example from a redirect, or <see langword="null"/> for none.</summary>
	[CascadingParameter(Name = "PageNotice")]
	public string? PageNotice { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the layout is disposed.</summary>
	private readonly CancellationTokenSource _lifetime = new();

	/// <summary>The boundary around the page, recovered on every navigation.</summary>
	private ErrorBoundary? _boundary;

	/// <summary>What the header, the tabs and the banner show; <see langword="null"/> until the workspace is loaded.</summary>
	private ShellViewModel? _shell;

	/// <summary>The id of the tab the address points to.</summary>
	private string _currentTab = "overview";

	/// <summary>The month the address asked for last, so the store is told only when it changes.</summary>
	private string? _lastRequestedMonth;

	/// <summary>Whether the layout has been disposed.</summary>
	private bool _disposed;
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override async Task OnInitializedAsync()
	{
		Store.Changed += OnStoreChanged;
		Navigation.LocationChanged += OnLocationChanged;
		ReadLocation();
		if (PageNotice is { Length: > 0 } notice)
		{
			ProblemNotices.Report(Notifications, new BackendProblem(400, "Request could not complete.", notice));
		}

		try
		{
			await Store.LoadAsync(_lifetime.Token);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		BuildShell();
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		Store.Changed -= OnStoreChanged;
		Navigation.LocationChanged -= OnLocationChanged;
		_lifetime.Cancel();
		_lifetime.Dispose();
		GC.SuppressFinalize(this);
	}
	#endregion

	#region Event Handlers
	/// <summary>Loads the workspace again after the owner clicked "Try again" on the backend status.</summary>
	/// <returns>A task that completes when the workspace was requested.</returns>
	private async Task RetryAsync()
	{
		try
		{
			await Store.RefreshAsync(_lifetime.Token);
		}
		catch (OperationCanceledException)
		{
			// The layout went away while the request ran; there is nothing left to show.
		}
	}

	/// <summary>Follows the address after a navigation.</summary>
	private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		_boundary?.Recover();
		ReadLocation();
		BuildShell();
		_ = InvokeAsync(StateHasChanged);
	}

	/// <summary>Rebuilds the frame and re-renders on the renderer's thread after the store changed.</summary>
	private void OnStoreChanged(object? sender, EventArgs e)
	{
		if (_disposed)
		{
			return;
		}

		_ = InvokeAsync(() =>
		{
			if (_disposed)
			{
				return;
			}

			BuildShell();
			StateHasChanged();
		});
	}
	#endregion

	#region Private Helpers
	/// <summary>Reads the tab and the month from the address and gives the month to the store when it changed.</summary>
	private void ReadLocation()
	{
		var uri = new Uri(Navigation.Uri);
		var segment = uri.AbsolutePath.Trim('/').Split('/')[0];
		_currentTab = segment.Length == 0 ? "overview" : segment;
		var month = HttpUtility.ParseQueryString(uri.Query)["m"];
		if (!string.Equals(month, _lastRequestedMonth, StringComparison.Ordinal))
		{
			_lastRequestedMonth = month;
			Store.SelectMonth(month);
		}
	}

	/// <summary>Builds the frame from the loaded workspace, or clears it when there is none.</summary>
	private void BuildShell() => _shell = Store.Analysis is { } analysis
		? new ShellViewModel(analysis, Store.Session, Store.Month, Formats.Belgian, Store.Now)
		: null;
	#endregion
}
