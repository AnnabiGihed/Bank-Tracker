namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Holds what every tab shows: the backend's workspace, rebuilt as the domain <see cref="Domain.Features.Shared.Services.Analysis"/>,
///              the bank session for display and the selected month. It loads the workspace once, reloads it after a
///              decision or a sync, and tells the UI through <see cref="Changed"/>. It stores nothing on the device.
/// </summary>
public sealed class WorkspaceStore : IDisposable
{
	#region Fields
	/// <summary>The typed client of the backend.</summary>
	private readonly BackendClient _backend;

	/// <summary>The clock that decides what today is.</summary>
	private readonly TimeProvider _clock;

	/// <summary>Lets one load or refresh run at a time, so two components that ask at once cause one request.</summary>
	private readonly SemaphoreSlim _gate = new(1, 1);

	/// <summary>The month the user asked for, which stays asked for even when a refresh makes it unavailable and back.</summary>
	private string? _requestedMonth;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="WorkspaceStore"/>.</summary>
	/// <param name="backend">The typed client of the backend.</param>
	/// <param name="clock">The clock that decides what today is, or <see langword="null"/> for the system clock.</param>
	/// <exception cref="ArgumentNullException"><paramref name="backend"/> is <see langword="null"/>.</exception>
	public WorkspaceStore(BackendClient backend, TimeProvider? clock = null)
	{
		ArgumentNullException.ThrowIfNull(backend);
		_backend = backend;
		_clock = clock ?? TimeProvider.System;
	}
	#endregion

	#region Events
	/// <summary>Occurs after the workspace, the selected month or the last problem changed. It can be raised on any thread.</summary>
	public event EventHandler? Changed;
	#endregion

	#region Properties
	/// <summary>Gets the analysis of the loaded workspace, or <see langword="null"/> before the first successful load.</summary>
	public Analysis? Analysis { get; private set; }

	/// <summary>Gets the bank session to display, or <see langword="null"/> when not connected or not loaded.</summary>
	public SessionInfo? Session { get; private set; }

	/// <summary>Gets the selected month as <c>yyyy-MM</c>; empty when there is no month.</summary>
	public string Month { get; private set; } = string.Empty;

	/// <summary>Gets a value indicating whether a workspace has been loaded.</summary>
	public bool IsLoaded => Analysis is not null;

	/// <summary>Gets the problem of the last load or refresh, or <see langword="null"/> when it succeeded.</summary>
	public BackendProblem? Problem { get; private set; }

	/// <summary>Gets today's date in the device's time zone.</summary>
	public DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

	/// <summary>Gets the current moment.</summary>
	public DateTimeOffset Now => _clock.GetUtcNow();
	#endregion

	#region Public Methods
	/// <summary>Loads the workspace unless it is loaded already.</summary>
	/// <param name="cancellationToken">Cancels the load.</param>
	/// <returns>The loaded snapshot, <see langword="null"/> as its value when it was loaded already, or the problem to show.</returns>
	public async Task<BackendResult<WorkspaceSnapshot>> LoadAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return IsLoaded ? new BackendResult<WorkspaceSnapshot>(null, null) : await ReloadAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Loads the workspace again, for after a decision or a sync, or when the owner asks.</summary>
	/// <param name="cancellationToken">Cancels the load.</param>
	/// <returns>The loaded snapshot, or the problem to show; on a problem the previous workspace stays.</returns>
	public async Task<BackendResult<WorkspaceSnapshot>> RefreshAsync(CancellationToken cancellationToken)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await ReloadAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>Selects a month. A month that is unknown falls back to the newest one.</summary>
	/// <param name="month">The month as <c>yyyy-MM</c>, or <see langword="null"/> for the newest.</param>
	public void SelectMonth(string? month)
	{
		_requestedMonth = month;
		Month = Analysis is null ? string.Empty : ResolveMonth(Analysis, month);
		OnChanged();
	}

	/// <summary>Sends one decision and, when the backend accepts it, reloads the workspace so every tab shows it. A failed
	/// reload after an accepted decision is reported through <see cref="Problem"/> and <see cref="Changed"/>, not through the returned result.</summary>
	/// <param name="request">The decision; build it with <see cref="DecisionRequests"/>.</param>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The backend's answer (the saved category id, or the assignment an undo needs), or the problem to show.</returns>
	public async Task<BackendResult<DecisionResponse>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
	{
		var result = await BackendCall.RunReplyAsync(ct => _backend.DecideAsync(request, ct), cancellationToken).ConfigureAwait(false);
		if (result.IsSuccess)
		{
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	/// <summary>Asks the backend for an incremental sync with the bank and, when it succeeds, reloads the workspace. A failed
	/// reload after an accepted sync is reported through <see cref="Problem"/> and <see cref="Changed"/>, not through the returned result.</summary>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The number of transactions added, or the problem to show.</returns>
	public Task<BackendResult<SyncSummary>> SyncAsync(CancellationToken cancellationToken) => RunSyncAsync(full: false, cancellationToken);

	/// <summary>Asks the backend to read the longest history the bank offers and, when it succeeds, reloads the workspace. A
	/// failed reload after an accepted sync is reported through <see cref="Problem"/> and <see cref="Changed"/>, not through the returned result.</summary>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The number of transactions added, or the problem to show.</returns>
	public Task<BackendResult<SyncSummary>> SyncFullAsync(CancellationToken cancellationToken) => RunSyncAsync(full: true, cancellationToken);

	/// <summary>Chooses the month a tab shows: the requested one when the analysis knows it, otherwise the newest.</summary>
	/// <param name="analysis">The analysis whose months are available.</param>
	/// <param name="requested">The requested month, or <see langword="null"/>.</param>
	/// <returns>The month as <c>yyyy-MM</c>, or an empty string when there is no month at all.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> is <see langword="null"/>.</exception>
	public static string ResolveMonth(Analysis analysis, string? requested)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		return requested is not null && analysis.AvailableMonths.Contains(requested) ? requested : analysis.AvailableMonths.FirstOrDefault() ?? string.Empty;
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose() => _gate.Dispose();
	#endregion

	#region Private Helpers
	/// <summary>Reads the workspace and applies it; the caller holds the gate. A failed read keeps what was loaded before.</summary>
	private async Task<BackendResult<WorkspaceSnapshot>> ReloadAsync(CancellationToken cancellationToken)
	{
		var result = await BackendCall.RunAsync(_backend.GetWorkspaceAsync, cancellationToken).ConfigureAwait(false);
		Problem = result.Problem;
		if (result.Value is { } snapshot)
		{
			Analysis = snapshot.ToAnalysis();
			Session = snapshot.ToDisplaySession();
			Month = ResolveMonth(Analysis, _requestedMonth);
		}

		OnChanged();
		return result;
	}

	/// <summary>Sends the sync request and reloads the workspace when the backend accepts it.</summary>
	private async Task<BackendResult<SyncSummary>> RunSyncAsync(bool full, CancellationToken cancellationToken)
	{
		var result = await BackendCall.RunReplyAsync(ct => _backend.SyncAsync(full, ct), cancellationToken).ConfigureAwait(false);
		if (result.IsSuccess)
		{
			await RefreshAsync(cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	/// <summary>Raises <see cref="Changed"/>.</summary>
	private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
	#endregion
}
