using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the typed backend client for either UI host with one set of rules: a checked base address,
///              a generous timeout for a sync, and the owner identity header when one is configured.
/// </summary>
public static class BackendClientRegistration
{
	#region Public Methods
	/// <summary>Registers <see cref="BackendClient"/> as a typed HTTP client. The settings are checked when the client
	/// is first created, so a host fails there, with a message that names the setting, rather than at startup.</summary>
	/// <param name="services">The service collection of the host.</param>
	/// <param name="options">Where the backend listens and who the owner is.</param>
	/// <returns>The builder of the client, so a host or a test can replace its message handler.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
	public static IHttpClientBuilder AddBackendClient(this IServiceCollection services, BackendOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);
		return services.AddHttpClient<BackendClient>(http =>
		{
			if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
				|| !string.IsNullOrEmpty(uri.UserInfo) || !uri.AbsolutePath.EndsWith('/'))
			{
				throw new InvalidOperationException("Configure Backend:BaseUrl as an HTTP base address ending in a slash.");
			}

			http.BaseAddress = uri;
			http.Timeout = TimeSpan.FromMinutes(5);
			if (!string.IsNullOrWhiteSpace(options.OwnerLogin))
			{
				http.DefaultRequestHeaders.Add(BackendOptions.OwnerHeader, options.OwnerLogin);
			}
		});
	}
	#endregion
}
