namespace KbcTracker.AppHost.Tests.Features.Orchestration;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the AppHost's resource graph without starting it: the start order that keeps the API from
///              running on an unmigrated database, the one-time fill commands that never run by themselves, and the
///              endpoints that must not be proxied because the hosts bind their own ports.
/// </summary>
public sealed class AppHostModelTests
{
	#region Model Tests
	/// <summary>Verifies the AppHost declares the database, the hosts and the maintenance commands.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_Resources_DatabaseHostsAndMaintenanceCommands()
	{
		var resources = await ReadResourcesAsync();

		resources.Select(r => r.Name).Should().Contain(["postgres-password", "postgres", "KbcTracker", "migrate", "import-json", "initialize", "api", "web"]);
	}

	/// <summary>Verifies the API waits for the migration to complete and the web app waits for a healthy API.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_StartOrder_ApiAfterMigrationWebAfterApi()
	{
		var resources = await ReadResourcesAsync();

		Waits(resources, "api").Should().Contain(("migrate", WaitType.WaitForCompletion));
		Waits(resources, "web").Should().Contain(("api", WaitType.WaitUntilHealthy));
		Waits(resources, "migrate").Should().Contain(("KbcTracker", WaitType.WaitUntilHealthy));
	}

	/// <summary>Verifies the import and the fresh start run only when started from the dashboard, never on start.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_OneTimeFill_StartedOnlyByHand()
	{
		var resources = await ReadResourcesAsync();

		Resource(resources, "import-json").Annotations.OfType<ExplicitStartupAnnotation>().Should().ContainSingle();
		Resource(resources, "initialize").Annotations.OfType<ExplicitStartupAnnotation>().Should().ContainSingle();
		Resource(resources, "migrate").Annotations.OfType<ExplicitStartupAnnotation>().Should().BeEmpty();
	}

	/// <summary>Verifies the API and the web app keep their own ports with no Aspire proxy in front, and the maintenance
	/// commands listen on nothing.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_Endpoints_HostsUnproxiedCommandsNone()
	{
		var resources = await ReadResourcesAsync();

		Endpoints(resources, "api").Should().ContainSingle(e => string.Equals(e.Name, "http", StringComparison.Ordinal) && !e.IsProxied && e.Port == 5080);
		Endpoints(resources, "web").Should().ContainSingle(e => string.Equals(e.Name, "https", StringComparison.Ordinal) && !e.IsProxied && e.Port == 5001);
		Endpoints(resources, "migrate").Should().BeEmpty();
	}

	/// <summary>Verifies each maintenance command passes exactly the one command the API accepts, the import with its
	/// folder; anything else makes the API exit with code 2 and, for the migration, keeps the backend from starting.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_MaintenanceCommands_ExactlyOneCommandEach()
	{
		var resources = await ReadResourcesAsync();

		(await ArgumentsAsync(Resource(resources, "migrate"))).Should().Equal("--migrate");
		(await ArgumentsAsync(Resource(resources, "initialize"))).Should().Equal("--initialize");
		var import = await ArgumentsAsync(Resource(resources, "import-json"));
		import.Should().HaveCount(2).And.StartWith("--import-json");
		import[1].Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>Verifies the history lives in a named volume of a container that outlives the AppHost.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Model_Postgres_PersistentWithNamedVolume()
	{
		var postgres = Resource(await ReadResourcesAsync(), "postgres");

		postgres.Annotations.OfType<ContainerLifetimeAnnotation>().Should().ContainSingle(l => l.Lifetime == ContainerLifetime.Persistent);
		postgres.Annotations.OfType<ContainerMountAnnotation>().Should().ContainSingle(m =>
			m.Type == ContainerMountType.Volume && string.Equals(m.Source, "kbctracker-postgres", StringComparison.Ordinal));
	}

	/// <summary>Verifies both hosts run in Development with forwarded headers off, so their loopback access checks see
	/// the real caller and the AppHost's health check is let in.</summary>
	/// <param name="name">The host's resource name.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("api")]
	[InlineData("web")]
	public async Task Model_Hosts_DevelopmentWithoutForwardedHeaders(string name)
	{
		var environment = await EnvironmentAsync(name);

		environment["ASPNETCORE_FORWARDEDHEADERS_ENABLED"].Should().Be("false");
		environment["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the AppHost's model without starting any resource.</summary>
	private static async Task<IReadOnlyList<IResource>> ReadResourcesAsync()
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.KbcTracker_AppHost>().ConfigureAwait(false);
		await using (builder.ConfigureAwait(false))
		{
			return [.. builder.Resources];
		}
	}

	/// <summary>Collects the command-line arguments a resource is started with.</summary>
	private static async Task<List<string?>> ArgumentsAsync(IResource resource)
	{
		var arguments = new List<object>();
		foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
		{
			await annotation.Callback(new CommandLineArgsCallbackContext(arguments, CancellationToken.None)).ConfigureAwait(false);
		}

		return [.. arguments.Select(a => a.ToString())];
	}

	/// <summary>Collects the plain-text environment variables a host is given, from the built model; values that refer to
	/// endpoints or other resources are left unevaluated, as nothing is started.</summary>
	private static async Task<Dictionary<string, object>> EnvironmentAsync(string name)
	{
		var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.KbcTracker_AppHost>().ConfigureAwait(false);
		await using (builder.ConfigureAwait(false))
		{
			var app = await builder.BuildAsync().ConfigureAwait(false);
			await using (app.ConfigureAwait(false))
			{
				var host = builder.Resources.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));
				var execution = new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)
				{
					Services = app.Services,
				});
				var environment = new Dictionary<string, object>(StringComparer.Ordinal);
				foreach (var annotation in host.Annotations.OfType<EnvironmentCallbackAnnotation>())
				{
					await annotation.Callback(new EnvironmentCallbackContext(execution, host, environment)).ConfigureAwait(false);
				}

				return environment;
			}
		}
	}

	/// <summary>Finds one resource by name.</summary>
	private static IResource Resource(IReadOnlyList<IResource> resources, string name) => resources.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));

	/// <summary>Lists what a resource waits for before it starts.</summary>
	private static List<(string Resource, WaitType Type)> Waits(IReadOnlyList<IResource> resources, string name) =>
		[.. Resource(resources, name).Annotations.OfType<WaitAnnotation>().Select(w => (w.Resource.Name, w.WaitType))];

	/// <summary>Lists a resource's declared endpoints.</summary>
	private static List<EndpointAnnotation> Endpoints(IReadOnlyList<IResource> resources, string name) =>
		[.. Resource(resources, name).Annotations.OfType<EndpointAnnotation>()];
	#endregion
}
