namespace KbcTracker.Components.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="KbcTrackerUiRegistration"/>: it registers the Radzen services, the workspace store with
///              the lifetime the host asks for (never transient) and the host's options.
/// </summary>
public sealed class KbcTrackerUiRegistrationTests
{
	#region Registration Tests
	/// <summary>Verifies a scoped store is one per scope and a singleton store is one for the whole app.</summary>
	/// <param name="lifetime">The lifetime the host asks for.</param>
	[Theory]
	[InlineData(ServiceLifetime.Scoped)]
	[InlineData(ServiceLifetime.Singleton)]
	public void AddKbcTrackerUi_Lifetime_RegistersTheStoreWithThatLifetime(ServiceLifetime lifetime)
	{
		using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5080/") };
		var services = new ServiceCollection().AddSingleton(new BackendClient(http));

		services.AddKbcTrackerUi(lifetime, options => options.ExportCsvPath = "/export.csv");

		services.Single(d => d.ServiceType == typeof(WorkspaceStore)).Lifetime.Should().Be(lifetime);
		using var provider = services.BuildServiceProvider();
		using var first = provider.CreateScope();
		using var second = provider.CreateScope();
		var inFirst = first.ServiceProvider.GetRequiredService<WorkspaceStore>();
		var inSecond = second.ServiceProvider.GetRequiredService<WorkspaceStore>();
		first.ServiceProvider.GetRequiredService<WorkspaceStore>().Should().BeSameAs(inFirst);
		ReferenceEquals(inFirst, inSecond).Should().Be(lifetime == ServiceLifetime.Singleton);
		services.Should().Contain(d => d.ServiceType == typeof(NotificationService));
		services.Should().Contain(d => d.ServiceType == typeof(DialogService));
		provider.GetRequiredService<KbcTrackerUiOptions>().ExportCsvPath.Should().Be("/export.csv");
	}

	/// <summary>Verifies a transient store is refused, because every component would load the workspace again.</summary>
	[Fact]
	public void AddKbcTrackerUi_Transient_Throws()
	{
		var act = () => new ServiceCollection().AddKbcTrackerUi(ServiceLifetime.Transient);

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("storeLifetime");
	}
	#endregion
}
