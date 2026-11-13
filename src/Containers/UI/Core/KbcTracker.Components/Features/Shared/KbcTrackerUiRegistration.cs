using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KbcTracker.Components.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers what the shared components need: the Radzen services, the <see cref="WorkspaceStore"/> and
///              the host's options. The view models are not registered: each one is built from the loaded analysis by
///              the page that shows it. A host registers <c>BackendClient</c> (<c>AddBackendClient</c>) and its
///              <see cref="IBankConnectionLauncher"/> itself.
/// </summary>
public static class KbcTrackerUiRegistration
{
	#region Public Methods
	/// <summary>Adds the shared UI services.</summary>
	/// <param name="services">The service collection of the host.</param>
	/// <param name="storeLifetime">How long the workspace store lives: <see cref="ServiceLifetime.Scoped"/> for the web app (one per circuit),
	/// <see cref="ServiceLifetime.Singleton"/> for the Android app (one for the whole app).</param>
	/// <param name="configure">Sets the host's options, or <see langword="null"/> for the defaults.</param>
	/// <returns>The same collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="storeLifetime"/> is <see cref="ServiceLifetime.Transient"/>, which would load the workspace for every component.</exception>
	public static IServiceCollection AddKbcTrackerUi(this IServiceCollection services, ServiceLifetime storeLifetime, Action<KbcTrackerUiOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		if (storeLifetime == ServiceLifetime.Transient)
		{
			throw new ArgumentOutOfRangeException(nameof(storeLifetime), storeLifetime, "The workspace store must be shared by every component.");
		}

		services.AddRadzenComponents();
		services.TryAddSingleton(TimeProvider.System);
		services.Add(new ServiceDescriptor(typeof(WorkspaceStore), typeof(WorkspaceStore), storeLifetime));
		var options = new KbcTrackerUiOptions();
		configure?.Invoke(options);
		services.AddSingleton(options);
		return services;
	}
	#endregion
}
