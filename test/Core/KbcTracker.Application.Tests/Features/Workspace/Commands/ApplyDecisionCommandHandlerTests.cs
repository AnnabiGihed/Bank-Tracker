using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Commands;
using KbcTracker.Application.Features.Workspace.Contracts;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Tests.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that <see cref="ApplyDecisionCommandHandler"/> waits a bounded time for the write lease held by
///              another decision, then applies the decision, fails with a conflict, or stops when the request is cancelled.
///
///              The persistence is an NSubstitute double of <see cref="IWorkspacePersistence"/>; the waits run on an
///              <see cref="InstantDelayClock"/>.
/// </summary>
public sealed class ApplyDecisionCommandHandlerTests
{
	#region Test Infrastructure
	/// <summary>A decision that only needs an initialised workspace: dismissing a suggestion.</summary>
	private static readonly ApplyDecisionCommand Dismiss = new(new DecisionRequest("dismiss", Key: "waiting-series"));

	/// <summary>The persistence double of the test.</summary>
	private readonly IWorkspacePersistence _persistence = Substitute.For<IWorkspacePersistence>();

	/// <summary>The clock the handler waits on.</summary>
	private readonly InstantDelayClock _clock = new();

	/// <summary>The lease the persistence hands out once it is free.</summary>
	private readonly IAsyncDisposable _lease = Substitute.For<IAsyncDisposable>();

	/// <summary>Initialises the test with an initialised, empty workspace whose saves succeed.</summary>
	public ApplyDecisionCommandHandlerTests()
	{
		_persistence.LoadForUpdateAsync(Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<WorkspaceData?>(new WorkspaceData([], [], UserData.Empty(), Session: null)));
		_persistence.SaveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success()));
	}

	/// <summary>Creates the handler over the persistence double and the instant clock.</summary>
	private ApplyDecisionCommandHandler Handler() => new(_persistence, _clock);

	/// <summary>Makes the lease busy for the given number of attempts, then free.</summary>
	private void LeaseFreeAfter(int busyAttempts)
	{
		var attempts = Enumerable.Repeat(Task.FromResult<IAsyncDisposable?>(null), busyAttempts)
			.Append(Task.FromResult<IAsyncDisposable?>(_lease)).ToArray();
		_persistence.TryAcquireWriteAsync(Arg.Any<CancellationToken>()).Returns(attempts[0], attempts[1..]);
	}
	#endregion

	#region Write Lease Tests
	/// <summary>Verifies a free lease is taken at once, without waiting.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Handle_LeaseFree_AppliesWithoutWaiting()
	{
		LeaseFreeAfter(busyAttempts: 0);

		var result = await Handler().Handle(Dismiss, CancellationToken.None);

		result.IsSuccess.Should().BeTrue();
		_clock.Elapsed.Should().Be(TimeSpan.Zero);
		await _persistence.Received(1).TryAcquireWriteAsync(Arg.Any<CancellationToken>());
	}

	/// <summary>Verifies a decision that finds the lease taken waits for it, then applies and saves the decision and
	/// releases the lease.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Handle_LeaseReleasedWhileWaiting_AppliesDecision()
	{
		LeaseFreeAfter(busyAttempts: 3);

		var result = await Handler().Handle(Dismiss, CancellationToken.None);

		result.IsSuccess.Should().BeTrue();
		_clock.Elapsed.Should().Be(ApplyDecisionCommandHandler.WriteRetryInterval * 3);
		await _persistence.Received(4).TryAcquireWriteAsync(Arg.Any<CancellationToken>());
		await _persistence.Received(1).SaveAsync(Arg.Any<CancellationToken>());
		await _lease.Received(1).DisposeAsync();
	}

	/// <summary>Verifies a decision gives up with a conflict once the lease stays taken for the whole wait, without
	/// loading or saving anything.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Handle_LeaseHeldPastLimit_ReturnsBusyConflict()
	{
		_persistence.TryAcquireWriteAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(null));

		var result = await Handler().Handle(Dismiss, CancellationToken.None);

		result.IsFailure.Should().BeTrue();
		result.Error.Code.Should().Be("Workspace.Busy");
		result.ResultExceptionType.Should().Be(ResultExceptionType.Conflict);
		_clock.Elapsed.Should().Be(ApplyDecisionCommandHandler.WriteWaitLimit);
		await _persistence.DidNotReceive().LoadForUpdateAsync(Arg.Any<CancellationToken>());
		await _persistence.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
	}

	/// <summary>Verifies cancelling the request stops the wait instead of trying again.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Handle_CancelledWhileWaiting_StopsWaiting()
	{
		using var cancellation = new CancellationTokenSource();
		_persistence.TryAcquireWriteAsync(Arg.Any<CancellationToken>()).Returns(_ =>
		{
			cancellation.Cancel();
			return Task.FromResult<IAsyncDisposable?>(null);
		});

		var handling = () => Handler().Handle(Dismiss, cancellation.Token);

		await handling.Should().ThrowAsync<OperationCanceledException>();
		await _persistence.Received(1).TryAcquireWriteAsync(Arg.Any<CancellationToken>());
		await _persistence.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
	}
	#endregion
}
