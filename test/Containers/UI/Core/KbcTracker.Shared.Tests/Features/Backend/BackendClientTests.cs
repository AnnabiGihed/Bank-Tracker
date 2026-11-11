using System.Net;
using System.Text;
using KbcTracker.Application.Features.BankConnection.Commands;
using KbcTracker.Application.Features.Workspace.Contracts;

namespace KbcTracker.Shared.Tests.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies how <see cref="BackendClient"/> turns backend answers into replies: success bodies, the
///              safe problem detail of a refusal, and a generic message for anything else.
/// </summary>
public sealed class BackendClientTests
{
	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Answers every request with one fixed response and remembers the request, in place of the backend.
	/// </summary>
	/// <param name="status">The status to answer with.</param>
	/// <param name="body">The body to answer with.</param>
	/// <param name="mediaType">The body's media type.</param>
	private sealed class StubHandler(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
	{
		#region Properties
		/// <summary>Gets the last request's path and query.</summary>
		public string? LastPath { get; private set; }

		/// <summary>Gets the last request's method.</summary>
		public HttpMethod? LastMethod { get; private set; }
		#endregion

		#region Overrides
		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			LastPath = request.RequestUri?.PathAndQuery;
			LastMethod = request.Method;
			return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
		}
		#endregion
	}
	#endregion

	#region Reply Tests
	/// <summary>Verifies a success body is read, and a full sync asks the versioned sync route with <c>full=true</c>.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_Success_ReadsBodyFromVersionedRoute()
	{
		using var handler = new StubHandler(HttpStatusCode.OK, """{"added":3}""", "application/json");
		var reply = await Client(handler).SyncAsync(full: true, CancellationToken.None);

		reply.IsSuccess.Should().BeTrue();
		reply.Value.Should().Be(new SyncSummary(3));
		reply.Error.Should().BeNull();
		handler.LastMethod.Should().Be(HttpMethod.Post);
		handler.LastPath.Should().Be("/api/v1/sync?full=true");
	}

	/// <summary>Verifies a refusal keeps the backend's status and its safe problem detail.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task DecideAsync_ProblemDetail_KeptWithStatus()
	{
		using var handler = new StubHandler(HttpStatusCode.Conflict,
			"""{"title":"Conflict","status":409,"detail":"The backend has no data yet."}""", "application/problem+json");
		var reply = await Client(handler).DecideAsync(new DecisionRequest("dismiss", Key: "series"), CancellationToken.None);

		reply.IsSuccess.Should().BeFalse();
		reply.StatusCode.Should().Be(409);
		reply.Error.Should().Be("The backend has no data yet.");
		reply.Value.Should().BeNull();
	}

	/// <summary>Verifies a problem without a detail, such as the backend's access refusal, is reported by its title
	/// rather than by the generic message.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task SyncAsync_ProblemWithoutDetail_TitleKept()
	{
		using var handler = new StubHandler(HttpStatusCode.Forbidden, """{"title":"Backend access denied.","status":403}""", "application/problem+json");
		var reply = await Client(handler).SyncAsync(full: false, CancellationToken.None);

		reply.StatusCode.Should().Be(403);
		reply.Error.Should().Be("Backend access denied.");
	}

	/// <summary>Verifies a failure that is not a problem document never surfaces its body, which may hold transport
	/// details, and gets a generic message instead.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task DecideAsync_NonProblemFailure_GenericMessage()
	{
		using var handler = new StubHandler(HttpStatusCode.BadGateway, "upstream connect error at 10.0.0.1", "text/plain");
		var reply = await Client(handler).DecideAsync(new DecisionRequest("dismiss", Key: "series"), CancellationToken.None);

		reply.StatusCode.Should().Be(502);
		reply.Error.Should().Be("The backend could not complete this request.");
	}

	/// <summary>Verifies a decision is posted to the versioned decisions route and its success body is read.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task DecideAsync_SuccessBody_Read()
	{
		using var handler = new StubHandler(HttpStatusCode.OK, """{"id":"groceries"}""", "application/json");
		var reply = await Client(handler).DecideAsync(new DecisionRequest("saveCategory", Name: "Groceries"), CancellationToken.None);

		reply.IsSuccess.Should().BeTrue();
		reply.Value!.Id.Should().Be("groceries");
		handler.LastPath.Should().Be("/api/v1/decisions");
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates a client over the stub, at a base address as the hosts configure it.</summary>
	private static BackendClient Client(StubHandler handler) =>
		new(new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:5080/") });
	#endregion
}
