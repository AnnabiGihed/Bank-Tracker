using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KbcTracker;

public sealed class EnableBankingOptions
{
    public string ApplicationId { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string RedirectUrl { get; set; } = "https://localhost:5001/callback";
    public string Country { get; set; } = "BE";
    public int ConsentDays { get; set; } = 180;
    public int HistoryMonths { get; set; } = 13;
}

/// <summary>Thin client over the Enable Banking REST API (read-only account information).</summary>
public sealed class EnableBankingClient(HttpClient http, EnableBankingOptions options)
{
    private const string BaseUrl = "https://api.enablebanking.com";

    // ---------- JWT (RS256), signed with the private key downloaded from the Control Panel ----------
    private string CreateJwt()
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "RS256", kid = options.ApplicationId });
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { iss = "enablebanking.com", aud = "api.enablebanking.com", iat = now, exp = now + 3600 });
        var unsigned = $"{B64(header)}.{B64(payload)}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(options.PrivateKeyPath));
        var sig = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{B64(sig)}";
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(method, BaseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt());
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Enable Banking {method} {path} failed ({(int)res.StatusCode}): {text}");
        return JsonNode.Parse(text)!;
    }

    /// <summary>Banks available in the configured country (we filter for KBC in the UI).</summary>
    public async Task<List<(string Name, string Country)>> GetBanksAsync()
    {
        var json = await SendAsync(HttpMethod.Get, $"/aspsps?country={options.Country}");
        return json["aspsps"]!.AsArray()
            .Select(a => (a!["name"]!.GetValue<string>(), a["country"]!.GetValue<string>()))
            .ToList();
    }

    /// <summary>Starts the consent flow. Returns the URL where you log in with KBC (itsme / card reader).</summary>
    public async Task<string> StartAuthAsync(string bankName, string country, string state)
    {
        var body = new
        {
            access = new { valid_until = DateTimeOffset.UtcNow.AddDays(options.ConsentDays).ToString("o") },
            aspsp = new { name = bankName, country },
            state,
            redirect_url = options.RedirectUrl,
            psu_type = "personal"
        };
        var json = await SendAsync(HttpMethod.Post, "/auth", body);
        return json["url"]!.GetValue<string>();
    }

    /// <summary>Exchanges the code from the redirect for a session + list of account UIDs.</summary>
    public async Task<(string SessionId, List<string> AccountUids, JsonNode Raw)> CreateSessionAsync(string code)
    {
        var json = await SendAsync(HttpMethod.Post, "/sessions", new { code });
        var sessionId = json["session_id"]?.GetValue<string>() ?? "";
        var uids = json["accounts"]!.AsArray()
            .Select(a => a is JsonValue v ? v.GetValue<string>() : a!["uid"]!.GetValue<string>())
            .ToList();
        return (sessionId, uids, json);
    }

    /// <summary>Downloads all transactions since <paramref name="from"/>, following continuation keys.</summary>
    public async Task<List<JsonNode>> GetTransactionsAsync(string accountUid, DateOnly from)
    {
        var all = new List<JsonNode>();
        string? continuation = null;
        do
        {
            var path = $"/accounts/{accountUid}/transactions?date_from={from:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(continuation)) path += $"&continuation_key={Uri.EscapeDataString(continuation)}";
            var json = await SendAsync(HttpMethod.Get, path);
            all.AddRange(json["transactions"]?.AsArray().Where(t => t is not null).Select(t => t!.DeepClone()) ?? []);
            continuation = json["continuation_key"]?.GetValue<string>();
        } while (!string.IsNullOrEmpty(continuation));
        return all;
    }

    public async Task<JsonNode> GetBalancesAsync(string accountUid) =>
        await SendAsync(HttpMethod.Get, $"/accounts/{accountUid}/balances");
}
