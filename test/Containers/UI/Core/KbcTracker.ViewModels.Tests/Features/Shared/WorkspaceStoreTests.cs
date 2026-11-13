using System.Net;

namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="WorkspaceStore"/> against a stubbed backend: it loads once, keeps the selected month
///              with the old fallback rule (an unknown month shows the newest), reloads after a decision or a sync, and
///              keeps what it had when the backend fails.
/// </summary>
public sealed class WorkspaceStoreTests
{
	#region Test Infrastructure
	/// <summary>The route the workspace is read from.</summary>
	private const string WorkspaceRoute = "/api/v1/workspace";

	/// <summary>Builds an analysis with a payment in October and one in November.</summary>
	private static Analysis TwoMonths() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"),
	]);

	/// <summary>Creates a stub that serves the two-month workspace.</summary>
	private static StubBackend Serving()
	{
		var backend = new StubBackend();
		backend.ServeWorkspace(TwoMonths());
		return backend;
	}
	#endregion

	#region Load Tests
	/// <summary>Verifies the workspace is requested once however often a tab asks, and exposes the analysis, the session and the newest month.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_CalledTwice_ReadsTheBackendOnce()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());

		var first = await store.LoadAsync(CancellationToken.None);
		var second = await store.LoadAsync(CancellationToken.None);

		first.IsSuccess.Should().BeTrue();
		second.IsSuccess.Should().BeTrue();
		backend.Count("GET", WorkspaceRoute).Should().Be(1);
		store.IsLoaded.Should().BeTrue();
		store.Analysis!.Items.Should().HaveCount(2);
		store.Session.Should().NotBeNull();
		store.Month.Should().Be("2026-11");
	}

	/// <summary>Verifies a disconnected workspace has no session.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_NotConnected_NoSession()
	{
		using var backend = new StubBackend();
		backend.ServeWorkspace(TwoMonths(), connected: false);
		using var store = new WorkspaceStore(backend.Client());

		await store.LoadAsync(CancellationToken.None);

		store.Session.Should().BeNull();
		store.Analysis.Should().NotBeNull();
	}

	/// <summary>Verifies a backend that is down gives a problem to show and leaves nothing loaded, and that a later
	/// refresh recovers and clears the problem.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_BackendDown_ProblemThenRecovers()
	{
		using var backend = Serving();
		backend.Unreachable = true;
		using var store = new WorkspaceStore(backend.Client());

		var down = await store.LoadAsync(CancellationToken.None);
		backend.Unreachable = false;
		var recovered = await store.RefreshAsync(CancellationToken.None);

		down.IsSuccess.Should().BeFalse();
		down.Problem!.Status.Should().Be(503);
		recovered.IsSuccess.Should().BeTrue();
		store.Problem.Should().BeNull();
		store.IsLoaded.Should().BeTrue();
	}

	/// <summary>Verifies a refused read shows the backend's reason and a failed refresh keeps the workspace already loaded.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RefreshAsync_Refused_KeepsPreviousWorkspaceAndShowsReason()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);
		backend.Respond(WorkspaceRoute, HttpStatusCode.Conflict, """{"title":"Conflict","status":409,"detail":"The backend has no data yet."}""", "application/problem+json");

		var refreshed = await store.RefreshAsync(CancellationToken.None);

		refreshed.IsSuccess.Should().BeFalse();
		store.Problem!.Status.Should().Be(409);
		store.Problem.Detail.Should().Be("The backend has no data yet.");
		store.Analysis!.Items.Should().HaveCount(2);
	}

	/// <summary>Verifies a cancelled load propagates the cancellation instead of becoming a problem.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task LoadAsync_Cancelled_Throws()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		var act = () => store.LoadAsync(cancelled.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}
	#endregion

	#region Month Tests
	/// <summary>Verifies a known month is kept, an unknown one or none shows the newest, as the old pages did.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SelectMonth_UnknownMonth_FallsBackToNewest()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);

		store.SelectMonth("2026-10");
		var known = store.Month;
		store.SelectMonth("1999-01");
		var unknown = store.Month;
		store.SelectMonth(null);

		known.Should().Be("2026-10");
		unknown.Should().Be("2026-11");
		store.Month.Should().Be("2026-11");
	}

	/// <summary>Verifies the chosen month survives a reload.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RefreshAsync_AfterSelectingAMonth_KeepsIt()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);
		store.SelectMonth("2026-10");

		await store.RefreshAsync(CancellationToken.None);

		store.Month.Should().Be("2026-10");
		backend.Count("GET", WorkspaceRoute).Should().Be(2);
	}

	/// <summary>Verifies the month rule on its own: a requested month the analysis knows, otherwise the newest, which for an
	/// analysis without payments is the current month.</summary>
	[Fact]
	public void ResolveMonth_KnownUnknownAndEmpty_Rule()
	{
		var analysis = TwoMonths();

		WorkspaceStore.ResolveMonth(analysis, "2026-10").Should().Be("2026-10");
		WorkspaceStore.ResolveMonth(analysis, "2030-01").Should().Be("2026-11");
		WorkspaceStore.ResolveMonth(analysis, null).Should().Be("2026-11");
		WorkspaceStore.ResolveMonth(TestWorkspace.Analyse([]), "2026-10").Should().Be("2026-11");
	}
	#endregion

	#region Action Tests
	/// <summary>Verifies an accepted decision is sent with its action and its answer returned, then the workspace is reloaded.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task DecideAsync_Accepted_SendsRequestAndReloads()
	{
		using var backend = Serving();
		backend.Respond("/api/v1/decisions", HttpStatusCode.OK, """{"id":"groceries"}""");
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);

		var result = await store.DecideAsync(DecisionRequests.SaveCategory(null, "Groceries", "Expense", string.Empty, "#123456", "ALDI"), CancellationToken.None);

		result.IsSuccess.Should().BeTrue();
		result.Value!.Id.Should().Be("groceries");
		backend.LastDecisionBody.Should().Contain("\"action\":\"saveCategory\"").And.Contain("\"name\":\"Groceries\"");
		backend.Count("GET", WorkspaceRoute).Should().Be(2);
	}

	/// <summary>Verifies a refused decision gives the backend's message and does not reload.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task DecideAsync_Refused_ProblemAndNoReload()
	{
		using var backend = Serving();
		backend.Respond("/api/v1/decisions", HttpStatusCode.NotFound, """{"title":"Not found","status":404,"detail":"This suggestion no longer exists."}""", "application/problem+json");
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);

		var result = await store.DecideAsync(DecisionRequests.Accept("SIGNATURE"), CancellationToken.None);

		result.IsSuccess.Should().BeFalse();
		result.Problem!.Status.Should().Be(404);
		result.Problem.Detail.Should().Be("This suggestion no longer exists.");
		backend.Count("GET", WorkspaceRoute).Should().Be(1);
	}

	/// <summary>Verifies a sync posts to the sync route (the full one on request), returns the count and reloads, and that
	/// a failed sync does not reload.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_SuccessAndFailure_ReloadsOnlyOnSuccess()
	{
		using var backend = Serving();
		backend.Respond("/api/v1/sync", HttpStatusCode.OK, """{"added":2}""");
		using var store = new WorkspaceStore(backend.Client());
		await store.LoadAsync(CancellationToken.None);

		var synced = await store.SyncFullAsync(CancellationToken.None);
		backend.Respond("/api/v1/sync", HttpStatusCode.Conflict, """{"title":"Conflict","status":409,"detail":"A sync is already running."}""", "application/problem+json");
		var refused = await store.SyncAsync(CancellationToken.None);

		synced.Value.Should().Be(new SyncSummary(2));
		refused.Problem!.Detail.Should().Be("A sync is already running.");
		backend.Count("POST", "/api/v1/sync").Should().Be(2);
		backend.Count("GET", WorkspaceRoute).Should().Be(2);
	}

	/// <summary>Verifies the store tells the UI when its data or its month changes.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Changed_LoadAndMonthSelection_Raised()
	{
		using var backend = Serving();
		using var store = new WorkspaceStore(backend.Client());
		var raised = 0;
		store.Changed += (_, _) => raised++;

		await store.LoadAsync(CancellationToken.None);
		store.SelectMonth("2026-10");

		raised.Should().Be(2);
	}
	#endregion
}
