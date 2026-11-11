using KbcTracker.Shared.Features.Backend;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<BackendClient>(http =>
{
	var address = builder.Configuration["Backend:BaseUrl"];
	if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
		|| !string.IsNullOrEmpty(uri.UserInfo) || !uri.AbsolutePath.EndsWith('/'))
	{
		throw new InvalidOperationException("Configure Backend:BaseUrl as an HTTP base address ending in a slash.");
	}

	http.BaseAddress = uri;
	http.Timeout = TimeSpan.FromMinutes(5);
	var owner = builder.Configuration["Backend:OwnerLogin"];
	if (!string.IsNullOrWhiteSpace(owner))
	{
		http.DefaultRequestHeaders.Add(WebAccessMiddleware.IdentityHeader, owner);
	}
});
builder.Services.AddProblemDetails();
var app = builder.Build();
if (args.Contains("--sync", StringComparer.Ordinal))
{
	await Console.Error.WriteLineAsync("The web host no longer syncs or stores bank data. Run the API service with --sync instead.").ConfigureAwait(false);
	return 2;
}

app.UseMiddleware<WebAccessMiddleware>();
app.UseMiddleware<BackendUnavailableMiddleware>();
WebEndpoints.Map(app);
await app.RunAsync().ConfigureAwait(false);
return 0;
