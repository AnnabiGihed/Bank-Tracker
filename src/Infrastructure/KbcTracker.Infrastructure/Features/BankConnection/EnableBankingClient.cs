using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KbcTracker.Application.Features.BankConnection.Consent;

namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Thin client over the Enable Banking REST API, limited to read-only account information: banks,
///              consent, session, transactions and balances.
///
///              Every request carries a JWT (RS256) signed with the application's private key, read from
///              <see cref="EnableBankingOptions.PrivateKeyPath"/> for each request so a replaced key is picked up
///              without a restart.
/// </summary>
public sealed class EnableBankingClient
{
	#region Fields
	private readonly HttpClient _http;
	private readonly EnableBankingOptions _options;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="EnableBankingClient"/>.
	/// </summary>
	/// <param name="http">The HTTP client used for every call.</param>
	/// <param name="options">The Enable Banking settings, including the private key path.</param>
	public EnableBankingClient(HttpClient http, EnableBankingOptions options)
	{
		_http = http;
		_options = options;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Lists the banks available in the configured country.
	/// </summary>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The name and country of each bank; the UI filters for KBC.</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status.</exception>
	public async Task<IReadOnlyList<(string Name, string Country)>> GetBanksAsync(CancellationToken cancellationToken = default)
	{
		var json = await SendAsync(HttpMethod.Get, $"/aspsps?country={_options.Country}", cancellationToken: cancellationToken).ConfigureAwait(false);
		return json["aspsps"]!.AsArray()
			.Select(bank => (bank!["name"]!.GetValue<string>(), bank["country"]!.GetValue<string>()))
			.ToList();
	}

	/// <summary>
	/// Starts the consent flow for a bank.
	/// </summary>
	/// <param name="bankName">The bank's name as returned by <see cref="GetBanksAsync"/>.</param>
	/// <param name="country">The bank's country.</param>
	/// <param name="state">A random value echoed back on the redirect, to tie it to this request.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The URL where the user logs in at the bank (itsme or card reader).</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status.</exception>
	public async Task<string> StartAuthAsync(string bankName, string country, string state, CancellationToken cancellationToken = default)
	{
		var body = new
		{
			access = new { valid_until = DateTimeOffset.UtcNow.AddDays(_options.ConsentDays).ToString("o", CultureInfo.InvariantCulture) },
			aspsp = new { name = bankName, country },
			state,
			redirect_url = _options.RedirectUrl,
			psu_type = "personal",
		};
		var json = await SendAsync(HttpMethod.Post, "/auth", body, cancellationToken).ConfigureAwait(false);
		return json["url"]!.GetValue<string>();
	}

	/// <summary>
	/// Exchanges the code from the consent redirect for a session.
	/// </summary>
	/// <param name="code">The <c>code</c> query parameter of the redirect.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The session id, the ids of the accounts the user gave access to, and the raw response.</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status.</exception>
	public async Task<(string SessionId, IList<string> AccountUids, JsonNode Raw)> CreateSessionAsync(string code, CancellationToken cancellationToken = default)
	{
		var json = await SendAsync(HttpMethod.Post, "/sessions", new { code }, cancellationToken).ConfigureAwait(false);
		var sessionId = json["session_id"]?.GetValue<string>() ?? "";
		var uids = json["accounts"]!.AsArray()
			.Select(account => account is JsonValue value ? value.GetValue<string>() : account!["uid"]!.GetValue<string>())
			.ToList();
		return (sessionId, uids, json);
	}

	/// <summary>
	/// Downloads an account's transactions, following continuation keys (a page can be empty while the API is still
	/// searching).
	/// </summary>
	/// <param name="accountUid">The account to read.</param>
	/// <param name="from">The first booking date asked for; only a hint when <paramref name="longest"/> is set.</param>
	/// <param name="longest"><see langword="true"/> for Enable Banking's <c>strategy=longest</c>: it finds the
	/// earliest transaction the bank will give and returns everything after it. Use it for the first or a full sync.</param>
	/// <returns>The transactions as returned by the API.</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status, for example when the bank
	/// refuses the requested period.</exception>
	public async Task<IReadOnlyList<JsonNode>> GetTransactionsAsync(string accountUid, DateOnly? from, bool longest = false)
	{
		var all = new List<JsonNode>();
		string? continuation = null;
		do
		{
			var json = await SendAsync(HttpMethod.Get, TransactionsPath(accountUid, from, longest, continuation)).ConfigureAwait(false);
			all.AddRange(json["transactions"]?.AsArray().OfType<JsonNode>().Select(transaction => transaction.DeepClone()) ?? []);
			continuation = json["continuation_key"]?.GetValue<string>();
		}
		while (!string.IsNullOrEmpty(continuation));
		return all;
	}

	/// <summary>
	/// Reads an account's balances.
	/// </summary>
	/// <param name="accountUid">The account to read.</param>
	/// <returns>The API response as JSON.</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status.</exception>
	public async Task<JsonNode> GetBalancesAsync(string accountUid) =>
		await SendAsync(HttpMethod.Get, $"/accounts/{accountUid}/balances").ConfigureAwait(false);
	#endregion

	#region Private Helpers
	/// <summary>
	/// Builds the transactions path with its query string.
	/// </summary>
	private static string TransactionsPath(string accountUid, DateOnly? from, bool longest, string? continuation)
	{
		var query = new List<string>();
		if (from is not null)
		{
			query.Add(string.Create(CultureInfo.InvariantCulture, $"date_from={from:yyyy-MM-dd}"));
		}

		if (longest)
		{
			query.Add("strategy=longest");
		}

		if (!string.IsNullOrEmpty(continuation))
		{
			query.Add($"continuation_key={Uri.EscapeDataString(continuation)}");
		}

		return $"/accounts/{accountUid}/transactions" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
	}

	/// <summary>
	/// Creates the JWT (RS256) that authenticates the application, valid for one hour.
	/// </summary>
	private string CreateJwt()
	{
		EnsureConfigured();
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var header = JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "RS256", kid = _options.ApplicationId });
		var payload = JsonSerializer.SerializeToUtf8Bytes(new { iss = "enablebanking.com", aud = "api.enablebanking.com", iat = now, exp = now + 3600 });
		var unsigned = $"{Base64Url(header)}.{Base64Url(payload)}";

		using var rsa = RSA.Create();
		rsa.ImportFromPem(File.ReadAllText(_options.PrivateKeyPath));
#pragma warning disable S5542 // RS256 is defined as RSASSA-PKCS1-v1_5 with SHA-256 (RFC 7518 §3.3), the only algorithm Enable Banking accepts; this is a signature, not encryption. Gihed Annabi 2026-11-09
		var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
#pragma warning restore S5542
		return $"{unsigned}.{Base64Url(signature)}";
	}

	/// <summary>
	/// Refuses to sign without an application id or a readable key file, naming the missing setting but never its value
	/// or the key's path, so the owner learns what to set up rather than seeing a provider failure.
	/// </summary>
	/// <exception cref="BankAccessNotConfiguredException">The application id is blank, or the key file is not set or not
	/// found.</exception>
	private void EnsureConfigured()
	{
		if (string.IsNullOrWhiteSpace(_options.ApplicationId))
		{
			throw new BankAccessNotConfiguredException("EnableBanking:ApplicationId is not set.");
		}

		if (string.IsNullOrWhiteSpace(_options.PrivateKeyPath) || !File.Exists(_options.PrivateKeyPath))
		{
			throw new BankAccessNotConfiguredException("EnableBanking:PrivateKeyPath is not set or the key file is not found.");
		}
	}

	/// <summary>
	/// Encodes bytes as unpadded base64url, as JWTs require.
	/// </summary>
	private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	/// <summary>
	/// Sends an authenticated request and parses the JSON response.
	/// </summary>
	/// <param name="method">The HTTP method.</param>
	/// <param name="path">The path relative to the API base address, with its query.</param>
	/// <param name="body">The JSON body, or <see langword="null"/> for none.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The parsed response.</returns>
	/// <exception cref="InvalidOperationException">The API answered with an error status; the message holds the
	/// method, path, status and response body.</exception>
	private async Task<JsonNode> SendAsync(HttpMethod method, string path, object? body = null, CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(method, new Uri(_options.BaseUrl, path));
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());
		if (body is not null)
		{
			request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
		}

		using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Enable Banking {method} {path} failed ({(int)response.StatusCode}): {text}");
		}

		return JsonNode.Parse(text)!;
	}
	#endregion
}
