// One start runs the whole app on this PC (ADR-0005): PostgreSQL, the migration, the seed, the API, then the web app.
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

// The secret of the API's "web" client (ADR-0009), generated once and kept in this project's user secrets like the
// password above. The API registers the client with it and the web app proves itself with it, so both get the same value
// and it never appears in a file of the repository. Regenerating it only needs the AppHost's secret deleted.
var webClientSecret = builder.AddParameter("web-client-secret", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true, persist: true);

// A new database gets the default categories and empty decisions; one that holds data is left alone, so this runs on
// every start and the API, which refuses writes until the database is filled, never waits for a manual step.
var seed = AddMaintenanceCommand("seed", "--initialize")
	.WaitForCompletion(migrate);

// The API binds its IPv4 loopback listener itself (BackendPort) and ignores ASPNETCORE_URLS, so Aspire must not put
// a proxy on the same port; the port comes from the launch profile so both always agree.
var api = builder.AddProject<Projects.KbcTracker_ApiService>("api")
	.WithEndpoint("http", endpoint => endpoint.IsProxied = false)
	.WithReference(database)
	.WithEnvironment("Authentication__Clients__Web__Secret", webClientSecret)
	.WaitForCompletion(seed)
	.WithHttpHealthCheck("/health");
var backend = api.GetEndpoint("http");
api.WithEnvironment("BackendPort", backend.Property(EndpointProperty.TargetPort));
RunAsLocalHost(api);

// The web app's own setting "Urls" fixes https://localhost:5001, which the browser and the bank's redirect use. It
// calls the API on 127.0.0.1, where the API listens, rather than "localhost", which tries IPv6 first. Its sign-in uses
// the address the browser reaches the API at, "localhost", because the provider (Google) accepts only that redirect
// address for the API's callback (Authentication:Authority).
var web = builder.AddProject<Projects.KbcTracker_Web>("web", launchProfileName: "https")
	.WithEndpoint("https", endpoint => endpoint.IsProxied = false)
	.WithEnvironment("Backend__BaseUrl", ReferenceExpression.Create($"http://127.0.0.1:{backend.Property(EndpointProperty.TargetPort)}/"))
	.WithEnvironment("Authentication__Authority", ReferenceExpression.Create($"http://localhost:{backend.Property(EndpointProperty.TargetPort)}/"))
	.WithEnvironment("Authentication__Clients__Web__Secret", webClientSecret)
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

// The API's sign-in keys come from the development certificates here. Forwarded headers would let any local caller rewrite
// the address a host sees, so they stay off whatever Aspire defaults to. Development is set explicitly, not left to the
// launch profile: outside it the API requires configured token keys.
static void RunAsLocalHost(IResourceBuilder<ProjectResource> host) =>
	host.WithEnvironment("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "false")
		.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
