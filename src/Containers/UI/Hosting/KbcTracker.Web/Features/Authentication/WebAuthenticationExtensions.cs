using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the web app's sign-in (ADR-0009): the cookie session, the OpenID Connect client toward the
///              API's token service (code flow with PKCE as the <c>web</c> client), the fallback policy that lets only the
///              owner reach anything, the revalidating authentication state of the circuit, and the token client that
///              signs the backend calls. The web app never sees a password or a provider token: the API signs the owner in
///              at the external provider.
/// </summary>
public static class WebAuthenticationExtensions
{
	#region Constants
	/// <summary>The request parameter through which the API's authorize endpoint learns which provider to use.</summary>
	private const string ProviderParameter = "provider";

	/// <summary>The path of the Blazor circuit, which cannot follow a redirect to a page.</summary>
	private const string CircuitPath = "/_blazor";
	#endregion

	#region Public Methods
	/// <summary>Registers the sign-in and returns the builder of the backend client's HTTP client so the caller can plug the token handler in.</summary>
	/// <param name="builder">The web host builder.</param>
	/// <returns>The builder of the backend client, with <see cref="ApiAccessTokenHandler"/> registered.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The client secret or the backend address is not configured, or an address is not https and not on this PC.</exception>
	public static IHttpClientBuilder AddWebAuthentication(this WebApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		var configuration = builder.Configuration;
		var secret = configuration[WebAuthentication.SecretSetting];
		if (string.IsNullOrWhiteSpace(secret))
		{
			throw new InvalidOperationException($"Configure {WebAuthentication.SecretSetting} (the secret of the API's web client, kept in secrets) before starting the web app.");
		}

		var backend = configuration["Backend:BaseUrl"];
		var authority = configuration[WebAuthentication.AuthoritySetting] is { Length: > 0 } configured ? configured : backend;
		var development = builder.Environment.IsDevelopment();
		var apiAddress = BaseAddress(backend);
		var authorityAddress = BaseAddress(authority);
		builder.Services.AddSingleton(new WebClientSecret(secret));
		builder.Services.TryAddSingleton(TimeProvider.System);
		builder.Services.AddSingleton<ApiTokenClient>();
		builder.Services.AddTransient<ApiAccessTokenHandler>();
		builder.Services.AddHttpClient(ApiTokenClient.HttpClientName, http => http.BaseAddress = apiAddress);
		builder.Services.AddAuthentication(options =>
			{
				options.DefaultScheme = WebAuthentication.CookieScheme;
				options.DefaultChallengeScheme = WebAuthentication.CookieScheme;
			})
			.AddCookie(WebAuthentication.CookieScheme, ConfigureCookie)
			.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options => ConfigureOpenIdConnect(options, authorityAddress, secret, development));
		builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder()
			.AddAuthenticationSchemes(WebAuthentication.CookieScheme)
			.RequireAuthenticatedUser()
			.RequireClaim(WebAuthentication.OwnerClaim, WebAuthentication.OwnerId)
			.Build());
		builder.Services.AddScoped<AuthenticationStateProvider, OwnerAuthenticationStateProvider>();
		return builder.Services.AddBackendClient(new BackendOptions(backend)).WithAccessTokenHandler<ApiAccessTokenHandler>();
	}
	#endregion

	#region Private Helpers
	/// <summary>Gives the session cookie its protections and its end, and keeps the owner out of the Blazor circuit with a status
	/// instead of a redirect.</summary>
	private static void ConfigureCookie(CookieAuthenticationOptions options)
	{
		options.Cookie.Name = WebAuthentication.CookieName;
		options.Cookie.HttpOnly = true;
		options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
		options.Cookie.SameSite = SameSiteMode.Lax;
		options.ExpireTimeSpan = WebAuthentication.SessionLifetime;

		// An absolute end: the session end is also a claim the circuit checks, and it must not move.
		options.SlidingExpiration = false;
		options.LoginPath = WebAuthentication.SignInPath;
		options.AccessDeniedPath = WebAuthentication.SignInPath;
		options.ReturnUrlParameter = WebAuthentication.ReturnUrlParameter;
		options.Events.OnSigningIn = context =>
		{
			var end = DateTimeOffset.UtcNow.Add(WebAuthentication.SessionLifetime).ToUnixTimeSeconds();
			context.Principal?.AddIdentity(new ClaimsIdentity([new Claim(WebAuthentication.SessionEndClaim, end.ToString(CultureInfo.InvariantCulture))]));
			return Task.CompletedTask;
		};
		options.Events.OnRedirectToLogin = context => Redirect(context, StatusCodes.Status401Unauthorized);
		options.Events.OnRedirectToAccessDenied = context => Redirect(context, StatusCodes.Status403Forbidden);
	}

	/// <summary>Answers the circuit and every non-navigation request with a status, and a page navigation with a redirect.</summary>
	private static Task Redirect(RedirectContext<CookieAuthenticationOptions> context, int status)
	{
		var request = context.Request;
		if (request.Path.StartsWithSegments(CircuitPath, StringComparison.Ordinal) || !HttpMethods.IsGet(request.Method))
		{
			context.Response.StatusCode = status;
			return Task.CompletedTask;
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	}

	/// <summary>Points the OpenID Connect client at the API as the <c>web</c> client: code flow with PKCE, the refresh token kept
	/// only for revocation, and the chosen provider passed on to the authorize endpoint.</summary>
	private static void ConfigureOpenIdConnect(OpenIdConnectOptions options, Uri address, string secret, bool development)
	{
		options.Authority = address.ToString();
		options.ClientId = WebAuthentication.ClientId;
		options.ClientSecret = secret;
		options.ResponseType = OpenIdConnectResponseType.Code;

		// A query response is a plain navigation back, which the cross-origin write check lets through; form_post is a cross-site POST.
		options.ResponseMode = OpenIdConnectResponseMode.Query;
		options.UsePkce = true;
		options.SignInScheme = WebAuthentication.CookieScheme;
		options.MapInboundClaims = false;
		options.GetClaimsFromUserInfoEndpoint = false;
		options.SaveTokens = false;
		options.RequireHttpsMetadata = !(development && IsLoopback(address));
		options.Scope.Clear();
		options.Scope.Add("openid");
		options.Scope.Add("offline_access");
		options.Events.OnRedirectToIdentityProvider = context =>
		{
			if (context.Properties.Items.TryGetValue(ProviderParameter, out var provider) && !string.IsNullOrEmpty(provider))
			{
				context.ProtocolMessage.SetParameter(ProviderParameter, provider);
			}

			return Task.CompletedTask;
		};
		options.Events.OnTokenValidated = context =>
		{
			if (context.TokenEndpointResponse?.RefreshToken is { Length: > 0 } refresh)
			{
				context.Properties?.StoreTokens([new AuthenticationToken { Name = WebAuthentication.RefreshTokenName, Value = refresh }]);
			}

			return Task.CompletedTask;
		};
		options.Events.OnRemoteFailure = context =>
		{
			context.Response.Redirect(WebAuthentication.SignInPath + "?error=failed");
			context.HandleResponse();
			return Task.CompletedTask;
		};
	}

	/// <summary>Reads a configured base address, with a message that names the setting when it is missing or malformed.</summary>
	private static Uri BaseAddress(string? value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) || !uri.AbsolutePath.EndsWith('/'))
		{
			throw new InvalidOperationException("Configure Backend:BaseUrl (and Authentication:Authority when set) as an HTTP base address ending in a slash.");
		}

		if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) && !IsLoopback(uri))
		{
			throw new InvalidOperationException("Backend:BaseUrl (and Authentication:Authority when set) must use https unless it points at this PC, because the owner's tokens travel there.");
		}

		return uri;
	}

	/// <summary>Whether an address points at this PC.</summary>
	private static bool IsLoopback(Uri address) =>
		address.IsLoopback || (IPAddress.TryParse(address.Host, out var ip) && IPAddress.IsLoopback(ip));
	#endregion
}
