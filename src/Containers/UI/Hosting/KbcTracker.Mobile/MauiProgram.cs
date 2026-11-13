using KbcTracker.Components.Features.Shared;
using KbcTracker.Shared.Features.Backend;
using Microsoft.Extensions.Configuration;

namespace KbcTracker.Mobile;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds the MAUI app: the Blazor web view, the settings embedded in the app, the typed backend client,
///              the shared UI services and the mobile bank-connection launcher. The CSV export stays web-only.
/// </summary>
public static class MauiProgram
{
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
		builder.Services.AddBackendClient(new BackendOptions(configuration["Backend:BaseUrl"], configuration["Backend:OwnerLogin"]));
		builder.Services.AddKbcTrackerUi(ServiceLifetime.Singleton);
		builder.Services.AddSingleton<IBankConnectionLauncher, MobileBankConnectionLauncher>();
		return builder.Build();
	}
	#endregion

	#region Private Helpers
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
