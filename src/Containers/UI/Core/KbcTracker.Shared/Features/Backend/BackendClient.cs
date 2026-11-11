using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.Application.Features.BankConnection.Commands;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.Workspace.Contracts;

namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Calls the central backend for UI reads, decisions and bank consent without local storage or bank keys.
/// </summary>
/// <param name="http">The configured backend HTTP client.</param>
public sealed class BackendClient(HttpClient http)
{
	#region Constants
	/// <summary>The versioned prefix of every backend route, relative to the configured base address.</summary>
	private const string Prefix = "api/v1/";
	#endregion

	#region Fields
	/// <summary>The backend's JSON format: web defaults, with enums as their names.</summary>
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Public Methods
	/// <summary>Reads the centrally computed workspace used by the existing pages.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The classified workspace.</returns>
	/// <exception cref="BackendRefusedException">The backend refused the request; carries its status and safe detail.</exception>
	/// <exception cref="HttpRequestException">The backend is unavailable or returned no workspace.</exception>
	public async Task<WorkspaceSnapshot> GetWorkspaceAsync(CancellationToken cancellationToken) =>
		await GetAsync<WorkspaceSnapshot>(Prefix + "workspace", cancellationToken).ConfigureAwait(false)
		?? throw new HttpRequestException("The backend returned no workspace.");

	/// <summary>Lists the supported banks without contacting a bank from the client.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The supported banks.</returns>
	/// <exception cref="BackendRefusedException">The backend refused the request, for example because bank access is not
	/// set up; carries its status and safe detail.</exception>
	/// <exception cref="HttpRequestException">The backend is unavailable.</exception>
	public async Task<IReadOnlyList<BankDefinition>> GetBanksAsync(CancellationToken cancellationToken) =>
		await GetAsync<List<BankDefinition>>(Prefix + "banks", cancellationToken).ConfigureAwait(false) ?? [];

	/// <summary>Persists one user decision on the backend.</summary>
	/// <param name="request">The action and its fields.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The action result or safe validation problem.</returns>
	public Task<BackendReply<DecisionResponse>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken) =>
		PostAsync<DecisionRequest, DecisionResponse>(Prefix + "decisions", request, cancellationToken);

	/// <summary>Creates a short-lived bank consent request.</summary>
	/// <param name="request">The selected bank.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The provider redirect or safe failure.</returns>
	public Task<BackendReply<ConsentStarted>> BeginConsentAsync(BeginConsentRequest request, CancellationToken cancellationToken) =>
		PostAsync<BeginConsentRequest, ConsentStarted>(Prefix + "consent", request, cancellationToken);

	/// <summary>Forwards the bank callback to the backend that owns its state and credentials.</summary>
	/// <param name="code">The authorization code, if consent succeeded.</param>
	/// <param name="state">The original one-use state.</param>
	/// <param name="error">The provider error code, if consent failed.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The connection status or safe failure.</returns>
	public async Task<BackendReply<ConnectionStatus>> CompleteConsentAsync(string? code, string? state, string? error, CancellationToken cancellationToken)
	{
		var path = $"{Prefix}consent/callback?code={Uri.EscapeDataString(code ?? string.Empty)}&state={Uri.EscapeDataString(state ?? string.Empty)}&error={Uri.EscapeDataString(error ?? string.Empty)}";
		using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
		return await ReadAsync<ConnectionStatus>(response, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Requests central sync, optionally refreshing the longest bank history.</summary>
	/// <param name="full">Whether to request the longest available history.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The durably added count or safe failure.</returns>
	public async Task<BackendReply<SyncSummary>> SyncAsync(bool full, CancellationToken cancellationToken)
	{
		using var response = await http.PostAsync(new Uri(Prefix + (full ? "sync?full=true" : "sync"), UriKind.Relative), null, cancellationToken).ConfigureAwait(false);
		return await ReadAsync<SyncSummary>(response, cancellationToken).ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>Reads a resource a page needs; a refusal throws with the backend's own detail, so the page can say why.</summary>
	private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
	{
		using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
		var reply = await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
		return reply.IsSuccess ? reply.Value : throw new BackendRefusedException(reply.Error ?? string.Empty, response.StatusCode);
	}

	/// <summary>Sends a typed mutation and disposes its transport response.</summary>
	private async Task<BackendReply<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken)
	{
		using var response = await http.PostAsJsonAsync(new Uri(path, UriKind.Relative), request, Json, cancellationToken).ConfigureAwait(false);
		return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Reads only the documented success body or safe problem detail.</summary>
	private static async Task<BackendReply<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return new((int)response.StatusCode, await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false), null);
		}

		var error = "The backend could not complete this request.";
		if (response.Content.Headers.ContentType?.MediaType is "application/problem+json")
		{
			using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken).ConfigureAwait(false);
			error = FindText(problem, "detail") ?? FindText(problem, "title") ?? error;
		}

		return new((int)response.StatusCode, default, error);
	}

	/// <summary>Finds a non-empty text member of a problem document; the title stands in when a problem, such as the
	/// backend's access refusal, carries no detail.</summary>
	private static string? FindText(JsonDocument? problem, string name) =>
		problem is not null && problem.RootElement.ValueKind is JsonValueKind.Object && problem.RootElement.TryGetProperty(name, out var value)
			&& value.ValueKind is JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
			? value.GetString()
			: null;
	#endregion
}
