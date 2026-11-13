using KbcTracker.ServiceDefaults.Features.Telemetry;
using KbcTracker.Shared.Features.Backend;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddBackendClient(new BackendOptions(builder.Configuration["Backend:BaseUrl"], builder.Configuration["Backend:OwnerLogin"]));
builder.Services.AddKbcTrackerUi(ServiceLifetime.Scoped, options => options.ExportCsvPath = "/export.csv");
builder.Services.AddScoped<IBankConnectionLauncher, WebBankConnectionLauncher>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddProblemDetails();
var app = builder.Build();
if (args.Contains("--sync", StringComparer.Ordinal))
{
	await Console.Error.WriteLineAsync("The web host no longer syncs or stores bank data. Run the API service with --sync instead.").ConfigureAwait(false);
	return 2;
}

app.UseMiddleware<WebAccessMiddleware>();
app.UseMiddleware<BackendUnavailableMiddleware>();
app.UseAntiforgery();
app.MapStaticAssets();
WebEndpoints.Map(app);
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode()
	.AddAdditionalAssemblies(typeof(KbcTrackerRouter).Assembly);
await app.RunAsync().ConfigureAwait(false);
return 0;
