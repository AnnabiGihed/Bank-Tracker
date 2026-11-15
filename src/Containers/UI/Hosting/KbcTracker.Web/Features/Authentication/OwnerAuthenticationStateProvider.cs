using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace KbcTracker.Web.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Re-checks the owner of a live Blazor circuit every few minutes. A circuit keeps the principal it started
///              with, so without this it could outlive the session cookie: the session's end is a claim, and once it has
///              passed (or the owner claim is missing) the connection is dropped.
/// </summary>
/// <param name="loggerFactory">Creates the logger of the base class.</param>
/// <param name="clock">The clock the session end is compared with.</param>
public sealed class OwnerAuthenticationStateProvider(ILoggerFactory loggerFactory, TimeProvider clock) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
	#region Properties
	/// <inheritdoc />
	protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);
	#endregion

	#region Public Methods
	/// <summary>Decides whether a principal is still the owner's open session.</summary>
	/// <param name="user">The principal the circuit started with.</param>
	/// <param name="now">The current time.</param>
	/// <returns><see langword="true"/> when the principal is authenticated, carries the owner claim and its session has not ended.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
	public static bool IsOpen(ClaimsPrincipal user, DateTimeOffset now)
	{
		ArgumentNullException.ThrowIfNull(user);
		var isOwner = user.Identity?.IsAuthenticated is true && user.HasClaim(WebAuthentication.OwnerClaim, WebAuthentication.OwnerId);
		var end = user.FindFirst(WebAuthentication.SessionEndClaim)?.Value;
		return isOwner && long.TryParse(end, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && now < DateTimeOffset.FromUnixTimeSeconds(seconds);
	}
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(authenticationState);
		return Task.FromResult(IsOpen(authenticationState.User, clock.GetUtcNow()));
	}
	#endregion
}
