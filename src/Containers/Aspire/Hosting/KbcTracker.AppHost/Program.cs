// One start runs the whole app on this PC (ADR-0005): PostgreSQL, the migration, the API, then the web app.
var builder = DistributedApplication.CreateBuilder(args);

// The password is generated once and kept in this project's user secrets: the data volume is initialised with it,
// so a new password on the next run would lock the history out.
var password = builder.AddParameter("postgres-password", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true, persist: true);

// The same pinned image as the PostgreSQL tests (ADR-0004). The container outlives the AppHost and keeps its data in
// a named volume, because the bank only returns about 90 days and the history must never be lost.
var postgres = builder.AddPostgres("postgres", password: password)
	.WithImageTag("17.6-alpine")
	.WithDataVolume("kbctracker-postgres")
	.WithLifetime(ContainerLifetime.Persistent);

// Named like the connection string the API already reads (ConnectionStrings:KbcTracker).
var database = postgres.AddDatabase("KbcTracker", "kbctracker");

// Migrations run before every start (a choice recorded in ADR-0005); the API starts only once they succeeded.
var migrate = AddMaintenanceCommand("migrate", "--migrate")
	.WaitFor(database);

// The one-time fill of a new database, started from the dashboard; both refuse a database that holds data.
var archive = builder.Configuration["KbcTracker:JsonArchive"]
	?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KbcTracker");
AddMaintenanceCommand("import-json", "--import-json", archive)
	.WaitForCompletion(migrate)
	.WithExplicitStart();
AddMaintenanceCommand("initialize", "--initialize")
	.WaitForCompletion(migrate)
	.WithExplicitStart();

// The API binds its IPv4 loopback listener itself (BackendPort) and ignores ASPNETCORE_URLS, so Aspire must not put
// a proxy on the same port; the port comes from the launch profile so both always agree.
var api = builder.AddProject<Projects.KbcTracker_ApiService>("api")
	.WithEndpoint("http", endpoint => endpoint.IsProxied = false)
	.WithReference(database)
	.WaitForCompletion(migrate)
	.WithHttpHealthCheck("/health");
var backend = api.GetEndpoint("http");
api.WithEnvironment("BackendPort", backend.Property(EndpointProperty.TargetPort));
RunAsLocalHost(api);

// The web app's own setting "Urls" fixes https://localhost:5001, which the browser and the bank's redirect use. It
// calls the API on 127.0.0.1, where the API listens, rather than "localhost", which tries IPv6 first.
var web = builder.AddProject<Projects.KbcTracker_Web>("web", launchProfileName: "https")
	.WithEndpoint("https", endpoint => endpoint.IsProxied = false)
	.WithEnvironment("Backend__BaseUrl", ReferenceExpression.Create($"http://127.0.0.1:{backend.Property(EndpointProperty.TargetPort)}/"))
	.WaitFor(api);
RunAsLocalHost(web);

await builder.Build().RunAsync().ConfigureAwait(false);

// Runs the API project once with one maintenance command against the database, then exits. No launch profile: it
// listens on nothing, so it never competes with the API for its port.
IResourceBuilder<ProjectResource> AddMaintenanceCommand(string name, params string[] commandArgs) =>
	builder.AddProject<Projects.KbcTracker_ApiService>(name, launchProfileName: null)
		.WithArgs(commandArgs)
		.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
		.WithReference(database);

// Both hosts decide access from the caller's address (a loopback peer). Forwarded headers would let any local caller
// rewrite that address, so they stay off whatever Aspire defaults to. Development is set explicitly, not left to the
// launch profile: outside it both hosts require the Tailscale owner header, which neither the browser on this PC nor
// the AppHost's health check sends, so the API would never turn healthy and the web app would never start.
static void RunAsLocalHost(IResourceBuilder<ProjectResource> host) =>
	host.WithEnvironment("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "false")
		.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
