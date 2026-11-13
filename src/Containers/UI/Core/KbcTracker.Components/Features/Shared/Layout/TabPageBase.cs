namespace KbcTracker.Components.Features.Shared.Layout;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The base of every tab page. It loads the workspace through the shared <see cref="WorkspaceStore"/> once,
///              rebuilds the page's view model when the analysis or the month changes, re-renders when the store raises
///              <c>Changed</c> (on any thread), sends decisions and reports a refused one, and cancels what is running
///              and drops the subscription when the page goes away.
/// </summary>
public abstract class TabPageBase : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the store that holds the workspace.</summary>
	[Inject]
	public required WorkspaceStore Store { get; set; }

	/// <summary>Gets or sets the service that shows notifications.</summary>
	[Inject]
	public required NotificationService Notifications { get; set; }

	/// <summary>Gets or sets the navigation manager.</summary>
	[Inject]
	public required NavigationManager Navigation { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the page is disposed, so a running call stops with its page.</summary>
	private readonly CancellationTokenSource _lifetime = new();

	/// <summary>The analysis the page was last built from.</summary>
	private Analysis? _builtFor;

	/// <summary>The month the page was last built for.</summary>
	private string _builtMonth = string.Empty;

	/// <summary>Whether the page has been disposed.</summary>
	private bool _disposed;
	#endregion

	#region Properties
	/// <summary>Gets the formats every page uses for amounts and dates.</summary>
	protected static Formats Formats => Formats.Belgian;

	/// <summary>Gets the token that is cancelled when the page goes away.</summary>
	protected CancellationToken Cancel => _lifetime.Token;

	/// <summary>Gets the selected month as <c>yyyy-MM</c>.</summary>
	protected string Month => Store.Month;
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override async Task OnInitializedAsync()
	{
		Store.Changed += OnStoreChanged;
		try
		{
			await Store.LoadAsync(Cancel);
		}
		catch (OperationCanceledException)
		{
			return;
		}

		EnsureBuilt();
	}

	/// <inheritdoc />
	protected override void OnParametersSet() => EnsureBuilt();
	#endregion

	#region Protected Methods
	/// <summary>Builds the page's view model from the loaded analysis.</summary>
	/// <param name="analysis">The analysis the store holds now.</param>
	protected abstract void Rebuild(Analysis analysis);

	/// <summary>Rebuilds the view model when the analysis or the month changed since the last build, or when forced.</summary>
	/// <param name="force">Rebuild even when nothing changed, for a page whose own parameters changed.</param>
	protected void EnsureBuilt(bool force = false)
	{
		if (Store.Analysis is not { } analysis)
		{
			return;
		}

		if (!force && ReferenceEquals(_builtFor, analysis) && string.Equals(_builtMonth, Store.Month, StringComparison.Ordinal))
		{
			return;
		}

		_builtFor = analysis;
		_builtMonth = Store.Month;
		Rebuild(analysis);
	}

	/// <summary>Sends one decision; the store reloads the workspace when the backend accepts it.</summary>
	/// <param name="request">The decision, built with <see cref="DecisionRequests"/>.</param>
	/// <returns>The backend's answer, or <see langword="null"/> when it refused (the problem is shown) or the page went away.</returns>
	protected async Task<DecisionResponse?> DecideAsync(DecisionRequest request)
	{
		try
		{
			var result = await Store.DecideAsync(request, Cancel);
			if (result.Problem is { } problem)
			{
				ProblemNotices.Report(Notifications, problem);
				return null;
			}

			return result.Value ?? new DecisionResponse();
		}
		catch (OperationCanceledException)
		{
			return null;
		}
	}

	/// <summary>Shows a notification that says the category now applies to the whole payee and offers to keep it to one payment only.</summary>
	/// <param name="payeeName">The payee's name, for the message.</param>
	/// <param name="categoryName">The name of the category the payee was given.</param>
	/// <param name="onlyThisOne">The decision that restores the payee and sets the category of this payment only.</param>
	protected void OfferOnlyThisOne(string payeeName, string categoryName, DecisionRequest onlyThisOne)
	{
		Notifications.Notify(new NotificationMessage
		{
			Severity = NotificationSeverity.Success,
			Summary = $"“{categoryName}” set for all payments to {payeeName}",
			DetailContent = _ => builder =>
			{
				builder.OpenComponent<OnlyThisOneNotice>(0);
				builder.AddAttribute(1, nameof(OnlyThisOneNotice.Apply), () => ApplyOnlyThisOneAsync(onlyThisOne));
				builder.CloseComponent();
			},
			Duration = 10000,
		});
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>Stops the subscription and cancels what is running.</summary>
	/// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing || _disposed)
		{
			return;
		}

		_disposed = true;
		Store.Changed -= OnStoreChanged;
		_lifetime.Cancel();
		_lifetime.Dispose();
	}
	#endregion

	#region Private Helpers
	/// <summary>Sends the "only this one" decision. It does not use the page's lifetime: the notification outlives the page, and the owner's click must still count.</summary>
	/// <param name="request">The decision that restores the payee and sets the category of this payment only.</param>
	/// <returns>A task that completes when the decision was sent.</returns>
	private async Task ApplyOnlyThisOneAsync(DecisionRequest request)
	{
		var result = await Store.DecideAsync(request, CancellationToken.None);
		if (result.Problem is { } problem)
		{
			ProblemNotices.Report(Notifications, problem);
		}
	}

	/// <summary>Rebuilds and re-renders on the renderer's thread after the store changed.</summary>
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

			EnsureBuilt();
			StateHasChanged();
		});
	}
	#endregion
}
