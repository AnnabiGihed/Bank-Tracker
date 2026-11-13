using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stands in for the backend's HTTP transport in the view model tests: it answers each versioned route
///              with a configured response, remembers the requests, and can act as an unreachable backend. The
///              workspace is served from a <see cref="WorkspaceSnapshot"/> in the backend's own JSON format.
/// </summary>
internal sealed class StubBackend : HttpMessageHandler
{
	#region Static Instances
	/// <summary>The backend's JSON format: web defaults, with enums as their names.</summary>
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Fields
	/// <summary>The configured answers by route.</summary>
	private readonly Dictionary<string, (HttpStatusCode Status, string Body, string MediaType)> _routes = new(StringComparer.Ordinal);
	#endregion

	#region Properties
	/// <summary>Gets the requests received, as <c>METHOD /path</c>, in order.</summary>
	public List<string> Requests { get; } = [];

	/// <summary>Gets or sets a value indicating whether every request fails as if the backend were not running.</summary>
	public bool Unreachable { get; set; }

	/// <summary>Gets the last body posted to the decisions route.</summary>
	public string? LastDecisionBody { get; private set; }
	#endregion

	#region Public Methods
	/// <summary>Creates a client over this stub, at the address the hosts configure.</summary>
	/// <returns>The client; the stub stays owned by the caller.</returns>
	public BackendClient Client() => new(new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:5080/") });

	/// <summary>Serves a workspace on the workspace route.</summary>
	/// <param name="analysis">The analysis to serve.</param>
	/// <param name="connected">Whether the bank is connected.</param>
	public void ServeWorkspace(Analysis analysis, bool connected = true) => Respond("/api/v1/workspace", HttpStatusCode.OK,
		JsonSerializer.Serialize(new WorkspaceSnapshot(AnalysisSnapshot.From(analysis, TestWorkspace.Today),
			new ConnectionStatus(connected, connected ? new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero) : null, connected ? new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero) : null, 0, false), connected ? 1 : 0), Json));

	/// <summary>Configures the answer of a route.</summary>
	/// <param name="path">The route, such as <c>/api/v1/decisions</c>.</param>
	/// <param name="status">The status to answer with.</param>
	/// <param name="body">The body.</param>
	/// <param name="mediaType">The body's media type.</param>
	public void Respond(string path, HttpStatusCode status, string body, string mediaType = "application/json") => _routes[path] = (status, body, mediaType);

	/// <summary>Counts the requests received for a method and route.</summary>
	/// <param name="method">The HTTP method.</param>
	/// <param name="path">The route.</param>
	/// <returns>How many requests matched.</returns>
	public int Count(string method, string path) => Requests.Count(r => string.Equals(r, method + " " + path, StringComparison.Ordinal));
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var path = request.RequestUri?.AbsolutePath ?? string.Empty;
		Requests.Add($"{request.Method} {path}");
		if (Unreachable)
		{
			throw new HttpRequestException("connection refused");
		}

		if (request.Content is not null && path.EndsWith("/decisions", StringComparison.Ordinal))
		{
			LastDecisionBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		var (status, body, mediaType) = _routes.GetValueOrDefault(path, (HttpStatusCode.NotFound, "{}", "application/json"));
		return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
	}
	#endregion
}
