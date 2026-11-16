using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.Shared.Features.Backend;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the typed backend client for either UI host with one set of rules: a checked base address,
///              and a generous timeout for a sync. The returned builder is the extension point of each host: the web app and
///              the Android app add a <see cref="DelegatingHandler"/> that sets the access token (see
///              <see cref="WithAccessTokenHandler{THandler}"/>).
/// </summary>
public static class BackendClientRegistration
{
	#region Public Methods
	/// <summary>Registers <see cref="BackendClient"/> as a typed HTTP client. The settings are checked when the client
	/// is first created, so a host fails there, with a message that names the setting, rather than at startup.</summary>
	/// <param name="services">The service collection of the host.</param>
	/// <param name="options">Where the backend listens.</param>
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
		});
	}

	/// <summary>Adds the host's handler that puts an access token on every backend request.</summary>
	/// <typeparam name="THandler">The handler, registered by the caller as a transient service.</typeparam>
	/// <param name="builder">The builder returned by <see cref="AddBackendClient"/>.</param>
	/// <returns>The same builder.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
	public static IHttpClientBuilder WithAccessTokenHandler<THandler>(this IHttpClientBuilder builder)
		where THandler : DelegatingHandler
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.AddHttpMessageHandler<THandler>();
	}
	#endregion
}
