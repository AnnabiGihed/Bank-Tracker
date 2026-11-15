using KbcTracker.ServiceDefaults.Features.Telemetry;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddWebAuthentication();
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

// The access check comes first so it covers the circuit and the sign-in; authentication, then the fallback policy (only
// the owner), then antiforgery, which must follow both.
app.UseMiddleware<WebAccessMiddleware>();
app.UseMiddleware<BackendUnavailableMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets().AllowAnonymous();
SignInEndpoints.Map(app);
WebEndpoints.Map(app);
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode()
	.AddAdditionalAssemblies(typeof(KbcTrackerRouter).Assembly);
await app.RunAsync().ConfigureAwait(false);
return 0;
