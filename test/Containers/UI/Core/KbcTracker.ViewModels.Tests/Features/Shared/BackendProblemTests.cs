using System.Net;
using System.Text.Json;

namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies how <see cref="BackendProblem"/> and <see cref="BackendCall"/> turn backend failures into the
///              messages the old error page showed: access refused (403), conflict with the backend's own reason (409),
///              anything else unavailable (503), and a refused action with its safe message.
/// </summary>
public sealed class BackendProblemTests
{
	#region Problem Tests
	/// <summary>Verifies a refused access, a conflict and a missing answer map to their statuses and messages.</summary>
	[Fact]
	public void FromException_ByStatus_MessagesOfTheOldErrorPage()
	{
		var forbidden = BackendProblem.FromException(new BackendRefusedException("denied", HttpStatusCode.Forbidden));
		var conflict = BackendProblem.FromException(new BackendRefusedException("Bank access is not set up on the backend.", HttpStatusCode.Conflict));
		var unreachable = BackendProblem.FromException(new HttpRequestException("connection refused 10.0.0.1"));

		forbidden.Status.Should().Be(403);
		forbidden.Title.Should().Be("Backend refused access.");
		forbidden.Detail.Should().Contain("Backend:OwnerLogin");
		conflict.Status.Should().Be(409);
		conflict.Title.Should().Be("The backend refused this request.");
		conflict.Detail.Should().Be("Bank access is not set up on the backend.");
		unreachable.Status.Should().Be(503);
		unreachable.Title.Should().Be("Backend unavailable.");
		unreachable.Detail.Should().Be("Start the backend and check its connection settings.").And.NotContain("10.0.0.1");
	}

	/// <summary>Verifies a conflict that carries no reason says so, instead of guessing a cause.</summary>
	[Fact]
	public void FromException_ConflictWithoutReason_SaysTheBackendGaveNone()
	{
		var problem = BackendProblem.FromException(new HttpRequestException("x", null, HttpStatusCode.Conflict));

		problem.Detail.Should().Be("The backend gave no reason. Check its log in the Aspire dashboard.");
	}

	/// <summary>Verifies a failed action keeps the reply's status and safe message, with a generic one when it has none.</summary>
	[Fact]
	public void FromReply_FailedAction_KeepsStatusAndSafeMessage()
	{
		var refused = BackendProblem.FromReply(new BackendReply<object>(409, null, "This suggestion no longer exists."));
		var silent = BackendProblem.FromReply(new BackendReply<object>(502, null, null));

		refused.Status.Should().Be(409);
		refused.Detail.Should().Be("This suggestion no longer exists.");
		silent.Detail.Should().Be("The backend could not complete this request.");
	}

	/// <summary>Verifies which exceptions are backend failures to show, and that a cancellation the caller asked for is not.</summary>
	[Fact]
	public void Handles_BackendFailuresOnly_NotACancellationTheCallerRequested()
	{
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();

		BackendProblem.Handles(new HttpRequestException(), CancellationToken.None).Should().BeTrue();
		BackendProblem.Handles(new JsonException(), CancellationToken.None).Should().BeTrue();
		BackendProblem.Handles(new TaskCanceledException(), CancellationToken.None).Should().BeTrue();
		BackendProblem.Handles(new TaskCanceledException(), cancelled.Token).Should().BeFalse();
		BackendProblem.Handles(new InvalidOperationException(), CancellationToken.None).Should().BeFalse();
	}
	#endregion

	#region Call Tests
	/// <summary>Verifies a read that succeeds returns its value, and one that throws a backend failure returns the problem.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RunAsync_SuccessAndBackendFailure_ValueOrProblem()
	{
		var ok = await BackendCall.RunAsync(_ => Task.FromResult(7), CancellationToken.None);
		var failed = await BackendCall.RunAsync<int>(_ => throw new HttpRequestException("down"), CancellationToken.None);

		ok.IsSuccess.Should().BeTrue();
		ok.Value.Should().Be(7);
		failed.IsSuccess.Should().BeFalse();
		failed.Problem!.Status.Should().Be(503);
	}

	/// <summary>Verifies a bug is not hidden as a backend problem, and a cancellation the caller asked for still propagates.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RunAsync_BugOrCancellation_Propagates()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		var bug = () => BackendCall.RunAsync<int>(_ => throw new InvalidOperationException("bug"), CancellationToken.None);
		var cancel = () => BackendCall.RunAsync<int>(_ => throw new TaskCanceledException(), cancelled.Token);

		await bug.Should().ThrowAsync<InvalidOperationException>();
		await cancel.Should().ThrowAsync<TaskCanceledException>();
	}

	/// <summary>Verifies a refused action comes back as the reply's problem and an accepted one as its value.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RunReplyAsync_RefusedAndAccepted_ProblemOrValue()
	{
		var accepted = await BackendCall.RunReplyAsync(_ => Task.FromResult(new BackendReply<SyncSummary>(200, new SyncSummary(3), null)), CancellationToken.None);
		var refused = await BackendCall.RunReplyAsync(_ => Task.FromResult(new BackendReply<SyncSummary>(409, null, "A sync is running.")), CancellationToken.None);

		accepted.Value.Should().Be(new SyncSummary(3));
		refused.IsSuccess.Should().BeFalse();
		refused.Problem!.Detail.Should().Be("A sync is running.");
		refused.Problem.Status.Should().Be(409);
	}
	#endregion
}
