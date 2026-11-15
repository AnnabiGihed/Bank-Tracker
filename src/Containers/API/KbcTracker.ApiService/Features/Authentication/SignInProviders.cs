using Microsoft.Extensions.DependencyInjection;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Knows the external providers the API can sign the owner in with. A provider is enabled by adding its
///              <c>Authentication:Providers:&lt;Name&gt;</c> section; supporting another one is one line in
///              <see cref="Registrars"/>.
/// </summary>
public static class SignInProviders
{
	#region Fields
	/// <summary>How each supported provider is registered, by the name OpenIddict knows it by.</summary>
	private static readonly Dictionary<string, Action<OpenIddictClientWebIntegrationBuilder, ProviderOptions>> Registrars =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["Google"] = (web, provider) => web.AddGoogle(google => google
				.SetClientId(provider.ClientId)
				.SetClientSecret(provider.ClientSecret)
				.SetRedirectUri(CallbackPath("Google"))
				.AddScopes(Scopes.OpenId)),
			["Microsoft"] = (web, provider) => web.AddMicrosoft(microsoft =>
			{
				microsoft.SetClientId(provider.ClientId)
					.SetClientSecret(provider.ClientSecret)
					.SetRedirectUri(CallbackPath("Microsoft"))
					.AddScopes(Scopes.OpenId);
				if (!string.IsNullOrWhiteSpace(provider.Tenant))
				{
					microsoft.SetTenant(provider.Tenant);
				}
			}),
		};
	#endregion

	#region Public Methods
	/// <summary>Finds the name a configured provider goes by, ignoring case.</summary>
	/// <param name="name">The name from configuration or a request.</param>
	/// <returns>The provider's canonical name, or <see langword="null"/> when it is not supported.</returns>
	public static string? Canonical(string? name) =>
		name is not null && Registrars.Keys.FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) is { } found ? found : null;

	/// <summary>Registers every configured provider with OpenIddict's client.</summary>
	/// <param name="web">The web-provider integration of the client.</param>
	/// <param name="providers">The configured providers.</param>
	/// <exception cref="InvalidOperationException">A configured provider is unsupported or lacks its client id or secret.</exception>
	public static void Register(OpenIddictClientWebIntegrationBuilder web, IEnumerable<KeyValuePair<string, ProviderOptions>> providers)
	{
		ArgumentNullException.ThrowIfNull(web);
		ArgumentNullException.ThrowIfNull(providers);
		foreach (var (name, provider) in providers)
		{
			if (!Registrars.TryGetValue(name, out var register))
			{
				throw new InvalidOperationException($"Authentication:Providers:{name} is not a supported provider. Supported: {string.Join(", ", Registrars.Keys)}.");
			}

			if (string.IsNullOrWhiteSpace(provider.ClientId) || string.IsNullOrWhiteSpace(provider.ClientSecret))
			{
				throw new InvalidOperationException($"Configure Authentication:Providers:{name}:ClientId and ClientSecret, or remove the section.");
			}

			register(web, provider);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>The relative redirect address a provider returns the owner to, resolved against the request's own address.</summary>
	private static Uri CallbackPath(string provider) => new("callback/login/" + provider.ToLowerInvariant(), UriKind.Relative);
	#endregion
}
