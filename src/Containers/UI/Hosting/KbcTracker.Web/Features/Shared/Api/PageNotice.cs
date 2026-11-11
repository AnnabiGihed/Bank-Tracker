namespace KbcTracker.Web.Features.Shared.Api;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries the message of a failed form action across its redirect, so the browser returns to the page it
///              came from with a notice instead of a raw problem document. The message lives in a short-lived cookie
///              and is shown once.
/// </summary>
public static class PageNotice
{
	#region Constants
	/// <summary>The cookie that holds the pending message.</summary>
	private const string CookieName = "kbc-notice";

	/// <summary>The longest message kept, so a verbose backend answer cannot outgrow a cookie.</summary>
	private const int MaxLength = 300;
	#endregion

	#region Static Instances
	/// <summary>How long an unread message survives; the redirect reads it at once, so this only bounds a lost one.</summary>
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
	#endregion

	#region Public Methods
	/// <summary>Stores a message for the next page the browser opens.</summary>
	/// <param name="response">The response that redirects back to the page.</param>
	/// <param name="message">The message to show; it is shortened to a cookie-safe length and encoded when shown.</param>
	public static void Set(HttpResponse response, string message)
	{
		ArgumentNullException.ThrowIfNull(response);
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		var options = CookieOptions();
		options.MaxAge = Lifetime;
		response.Cookies.Append(CookieName, Shorten(message), options);
	}

	/// <summary>Reads the pending message and removes it, so it is shown only once.</summary>
	/// <param name="context">The request of the page being rendered.</param>
	/// <returns>The message, or <see langword="null"/> when none is pending.</returns>
	public static string? Take(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (!context.Request.Cookies.TryGetValue(CookieName, out var message) || string.IsNullOrWhiteSpace(message))
		{
			return null;
		}

		context.Response.Cookies.Delete(CookieName, CookieOptions());
		return Shorten(message);
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the cookie settings: script-proof, HTTPS only (the web app is served only over HTTPS), and Lax
	/// rather than Strict, because the bank's callback is a cross-site navigation and a Strict cookie set on it would
	/// not be sent with the redirect that follows.</summary>
	private static CookieOptions CookieOptions() => new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/" };

	/// <summary>Cuts a message to the length a cookie can safely carry.</summary>
	private static string Shorten(string message) => message.Length > MaxLength ? message[..MaxLength] : message;
	#endregion
}
