using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The three routes of the owner's session that are reachable without being signed in: the static sign-in
///              page, the redirect that starts the sign-in with one provider, and the sign-out. The providers the page
///              offers come from this host's <c>SignIn:Providers</c> setting (the API has no provider list to ask for); the
///              API still refuses a provider it has not configured.
/// </summary>
public static partial class SignInEndpoints
{
	#region Public Methods
	/// <summary>Maps the sign-in page, the sign-in start and the sign-out.</summary>
	/// <param name="app">The web host.</param>
	/// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
	public static void Map(WebApplication app)
	{
		ArgumentNullException.ThrowIfNull(app);
		app.MapGet(WebAuthentication.SignInPath, ShowSignIn).AllowAnonymous();
		app.MapGet(WebAuthentication.SignInStartPath, StartSignIn).AllowAnonymous();
		app.MapPost(WebAuthentication.SignOutPath, SignOutAsync).AllowAnonymous();
	}

	/// <summary>Accepts only a page of this site as the place to return to, so the sign-in cannot be used to send the owner elsewhere.</summary>
	/// <param name="returnUrl">The requested address.</param>
	/// <returns>The address when it is a local path, otherwise <c>/</c>.</returns>
	public static string LocalOrRoot(string? returnUrl) =>
		IsLocalPath(returnUrl) ? returnUrl! : "/";
	#endregion

	#region Private Helpers
	/// <summary>The framework's local-URL rule: one leading slash not followed by a slash or backslash, and no control characters.</summary>
	private static bool IsLocalPath(string? url) =>
		url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\' && !url.Any(char.IsControl)));

	/// <summary>Shows the providers to sign in with, or goes on when the owner is already signed in.</summary>
	private static async Task<IResult> ShowSignIn(HttpContext context, IConfiguration configuration, string? returnUrl, string? error)
	{
		var destination = LocalOrRoot(returnUrl);
		if ((await context.AuthenticateAsync(WebAuthentication.CookieScheme).ConfigureAwait(false)).Succeeded)
		{
			return Results.LocalRedirect(destination);
		}

		return new RazorComponentResult<SignInPage>(new
		{
			Providers = Providers(configuration),
			ReturnUrl = destination,
			Failed = !string.IsNullOrEmpty(error),
		});
	}

	/// <summary>Sends the browser to the API's sign-in with the chosen provider, and back to the page it came from.</summary>
	private static IResult StartSignIn(IConfiguration configuration, string? provider, string? returnUrl)
	{
		var chosen = Providers(configuration).FirstOrDefault(name => string.Equals(name, provider, StringComparison.OrdinalIgnoreCase));
		if (chosen is null)
		{
			return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown sign-in provider.");
		}

		var properties = new AuthenticationProperties { RedirectUri = LocalOrRoot(returnUrl) };
		properties.Items["provider"] = chosen;
		return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
	}

	/// <summary>Ends the session here and revokes the refresh token at the API; a request without a valid antiforgery token is refused.</summary>
	private static async Task<IResult> SignOutAsync(HttpContext context, IAntiforgery antiforgery, ApiTokenClient tokens, ILoggerFactory loggers)
	{
		try
		{
			await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
		}
		catch (AntiforgeryValidationException)
		{
			return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The sign-out request is not valid.");
		}

		var refresh = await context.GetTokenAsync(WebAuthentication.CookieScheme, WebAuthentication.RefreshTokenName).ConfigureAwait(false);
		await context.SignOutAsync(WebAuthentication.CookieScheme).ConfigureAwait(false);
		if (!string.IsNullOrEmpty(refresh))
		{
			try
			{
				await tokens.RevokeRefreshTokenAsync(refresh, context.RequestAborted).ConfigureAwait(false);
			}
			catch (HttpRequestException exception)
			{
				// The session here is already gone; an unreachable API must not keep the owner signed in. The token expires by itself.
				LogRevokeFailed(loggers.CreateLogger(typeof(SignInEndpoints)), exception);
			}
		}

		return Results.Redirect(WebAuthentication.SignInPath);
	}

	/// <summary>Records that the API could not revoke the refresh token at sign-out.</summary>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The refresh token could not be revoked at the API.")]
	private static partial void LogRevokeFailed(ILogger logger, Exception exception);

	/// <summary>Reads the providers the page offers.</summary>
	private static string[] Providers(IConfiguration configuration) =>
		[.. (configuration.GetSection(WebAuthentication.ProvidersSetting).Get<string[]>() ?? []).Where(name => !string.IsNullOrWhiteSpace(name))];
	#endregion
}
