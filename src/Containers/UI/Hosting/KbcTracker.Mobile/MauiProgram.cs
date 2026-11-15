using KbcTracker.Components.Features.Shared;
using KbcTracker.Mobile.Features.Authentication;
using KbcTracker.Shared.Features.Authentication;
using KbcTracker.Shared.Features.Backend;
using Microsoft.Extensions.Configuration;

namespace KbcTracker.Mobile;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds the MAUI app: the Blazor web view, the settings embedded in the app, the owner's sign-in (token
///              logic, secure refresh token storage, the handler that puts the access token on backend calls), the typed
///              backend client, the shared UI services and the mobile bank-connection launcher. The CSV export stays web-only.
/// </summary>
public static class MauiProgram
{
	#region Constants
	/// <summary>The client id the API registered for this app.</summary>
	private const string ClientId = "mobile";

	/// <summary>The redirect address the API registered for this app, which <see cref="WebAuthenticationCallbackActivity"/> claims.</summary>
	private const string RedirectAddress = "com.banktracker.mobile://auth";
	#endregion

	#region Public Methods
	/// <summary>Creates the app and registers its services.</summary>
	/// <returns>The built MAUI app.</returns>
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();
		builder.Services.AddMauiBlazorWebView();
		AddSettings(builder.Configuration);
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif
		var configuration = builder.Configuration;
		AddSignIn(builder.Services, configuration);
		builder.Services.AddBackendClient(new BackendOptions(configuration["Backend:BaseUrl"])).WithAccessTokenHandler<AccessTokenHandler>();
		builder.Services.AddKbcTrackerUi(ServiceLifetime.Singleton);
		builder.Services.AddSingleton<IBankConnectionLauncher, MobileBankConnectionLauncher>();
		return builder.Build();
	}
	#endregion

	#region Private Helpers
	/// <summary>Registers the owner's sign-in. The token service reaches the API with a client that carries no access token, so a refresh can never wait on itself.</summary>
	/// <param name="services">The service collection of the app.</param>
	/// <param name="configuration">The settings of the app.</param>
	private static void AddSignIn(IServiceCollection services, ConfigurationManager configuration)
	{
		services.AddSingleton(_ => new PublicClientOptions(AuthorityFrom(configuration["Backend:BaseUrl"]), ClientId, new Uri(RedirectAddress)));
		services.AddHttpClient(PublicClientTokenService.HttpClientName, (provider, http) => http.BaseAddress = provider.GetRequiredService<PublicClientOptions>().Authority);
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton<IRefreshTokenStore, SecureStorageRefreshTokenStore>();
		services.AddSingleton<PublicClientTokenService>();
		services.AddSingleton(new MobileSignInOptions([.. (configuration.GetSection("SignIn:Providers").Get<string[]>() ?? []).Where(name => !string.IsNullOrWhiteSpace(name))]));
		services.AddSingleton<MobileSession>();
		services.AddTransient<AccessTokenHandler>();
	}

	/// <summary>Reads the backend address the token service is below.</summary>
	/// <param name="baseUrl">The <c>Backend:BaseUrl</c> setting.</param>
	/// <returns>The address.</returns>
	/// <exception cref="InvalidOperationException">The setting is not an absolute address.</exception>
	private static Uri AuthorityFrom(string? baseUrl) =>
		Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
			? uri
			: throw new InvalidOperationException("Configure Backend:BaseUrl as an HTTP base address ending in a slash.");

	/// <summary>Adds the settings embedded in the app: always <c>appsettings.json</c>, and in Debug builds also <c>appsettings.Development.json</c>.</summary>
	/// <param name="configuration">The configuration of the app being built.</param>
	/// <exception cref="InvalidOperationException">An embedded settings file is missing.</exception>
	private static void AddSettings(IConfigurationBuilder configuration)
	{
		AddEmbeddedSettings(configuration, "appsettings.json");
#if DEBUG
		AddEmbeddedSettings(configuration, "appsettings.Development.json");
#endif
	}

	/// <summary>Adds one embedded JSON settings file. The file is read completely before its stream is disposed.</summary>
	/// <param name="configuration">The configuration of the app being built.</param>
	/// <param name="resourceName">The name of the embedded resource.</param>
	/// <exception cref="InvalidOperationException">The resource is not embedded in the app.</exception>
	private static void AddEmbeddedSettings(IConfigurationBuilder configuration, string resourceName)
	{
		using var stream = typeof(MauiProgram).Assembly.GetManifestResourceStream(resourceName)
			?? throw new InvalidOperationException($"The settings file {resourceName} is not embedded in the app.");
		var loaded = new ConfigurationBuilder().AddJsonStream(stream).Build();
		configuration.AddConfiguration(loaded);
	}
	#endregion
}
