using System.Text.Json;

namespace KbcTracker.Shared.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the API's token endpoint answers with on success: the access token, how long it lasts and, when the
///              grant allows it, the next refresh token. <see cref="Parse"/> accepts nothing without an access token and a
///              positive lifetime, so a broken answer never becomes a session.
/// </summary>
/// <param name="AccessToken">The bearer token for the API.</param>
/// <param name="RefreshToken">The refresh token, or <see langword="null"/> when the response carries none.</param>
/// <param name="Lifetime">How long the access token is valid from now.</param>
public sealed record TokenResponse(string AccessToken, string? RefreshToken, TimeSpan Lifetime)
{
	#region Public Methods
	/// <summary>Reads a token endpoint response body.</summary>
	/// <param name="json">The JSON body.</param>
	/// <returns>The parsed response.</returns>
	/// <exception cref="InvalidOperationException">The body is not JSON, or has no access token or lifetime.</exception>
	public static TokenResponse Parse(string json)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException exception)
		{
			throw new InvalidOperationException("The token response is not valid JSON.", exception);
		}

		using (document)
		{
			var root = document.RootElement;
			var access = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String ? token.GetString() : null;
			if (string.IsNullOrEmpty(access) || !root.TryGetProperty("expires_in", out var seconds) || !seconds.TryGetInt32(out var lifetime) || lifetime <= 0)
			{
				throw new InvalidOperationException("The token response has no access token or lifetime.");
			}

			var refresh = root.TryGetProperty("refresh_token", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
			return new TokenResponse(access, string.IsNullOrEmpty(refresh) ? null : refresh, TimeSpan.FromSeconds(lifetime));
		}
	}
	#endregion
}
