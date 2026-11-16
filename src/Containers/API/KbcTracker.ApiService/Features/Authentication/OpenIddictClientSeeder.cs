using Npgsql;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the two clients of the token service, <c>web</c> (confidential) and <c>mobile</c> (public), in
///              the database, creating or updating them so every start leaves them as configured. A database that is not
///              migrated yet is skipped with a warning, so the maintenance commands that migrate it can still run; the
///              next start seeds it.
/// </summary>
/// <param name="services">The service provider, for a scope per run.</param>
/// <param name="options">The configured clients.</param>
/// <param name="logger">The logger for the skipped case.</param>
public sealed partial class OpenIddictClientSeeder(IServiceProvider services, OwnerSignInOptions options, ILogger<OpenIddictClientSeeder> logger) : IHostedService
{
	#region Constants
	/// <summary>The PostgreSQL error code for a table that does not exist.</summary>
	private const string UndefinedTable = "42P01";

	/// <summary>The PostgreSQL error code for a database that does not exist yet; <c>--migrate</c> creates it.</summary>
	private const string UndefinedDatabase = "3D000";

	/// <summary>The port of the web app, fixed by its own setting <c>Urls</c>.</summary>
	private const int WebPort = 5001;

	/// <summary>The hosts a plain-http redirect address may use.</summary>
	private static readonly HashSet<string> LoopbackHosts = new(["localhost", "127.0.0.1", "[::1]"], StringComparer.OrdinalIgnoreCase);
	#endregion

	#region IHostedService Implementation
	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			await SeedAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (IsNotMigrated(exception))
		{
			LogNotMigrated(exception);
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	#endregion

	#region Public Methods
	/// <summary>Creates or updates both clients.</summary>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>A task that completes when both are stored.</returns>
	/// <exception cref="InvalidOperationException">The web client's secret or an extra redirect address is not configured correctly.</exception>
	public async Task SeedAsync(CancellationToken cancellationToken)
	{
		var web = WebClient();
		var scope = services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
			await UpsertAsync(manager, web, cancellationToken).ConfigureAwait(false);
			await UpsertAsync(manager, MobileClient(), cancellationToken).ConfigureAwait(false);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Describes the confidential client of the web app.</summary>
	private OpenIddictApplicationDescriptor WebClient()
	{
		if (string.IsNullOrWhiteSpace(options.Clients.Web.Secret))
		{
			throw new InvalidOperationException("Configure Authentication:Clients:Web:Secret (a long random value, kept in secrets) before starting the backend.");
		}

		var descriptor = new OpenIddictApplicationDescriptor
		{
			ClientId = OwnerIdentity.WebClientId,
			ClientSecret = options.Clients.Web.Secret,
			ClientType = ClientTypes.Confidential,
			ConsentType = ConsentTypes.Implicit,
			DisplayName = "KbcTracker web app",
		};
		descriptor.RedirectUris.Add(new UriBuilder(Uri.UriSchemeHttps, "localhost", WebPort, "signin-oidc").Uri);
		descriptor.PostLogoutRedirectUris.Add(new UriBuilder(Uri.UriSchemeHttps, "localhost", WebPort).Uri);
		foreach (var extra in options.Clients.Web.ExtraRedirectUris)
		{
			descriptor.RedirectUris.Add(Uri.TryCreate(extra, UriKind.Absolute, out var uri) && IsSecureRedirect(uri)
				? uri
				: throw new InvalidOperationException("Authentication:Clients:Web:ExtraRedirectUris must hold https addresses, or http ones on localhost, 127.0.0.1 or [::1]."));
		}

		AddCodeFlowPermissions(descriptor);
		descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
		return descriptor;
	}

	/// <summary>Whether a redirect address is safe to hand a code to: https, or plain http only on a loopback host.</summary>
	private static bool IsSecureRedirect(Uri uri) =>
		string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
		|| (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && LoopbackHosts.Contains(uri.Host));

	/// <summary>Describes the public client of the Android app, which proves itself with PKCE only.</summary>
	private static OpenIddictApplicationDescriptor MobileClient()
	{
		var descriptor = new OpenIddictApplicationDescriptor
		{
			ClientId = OwnerIdentity.MobileClientId,
			ClientType = ClientTypes.Public,
			ConsentType = ConsentTypes.Implicit,
			DisplayName = "KbcTracker Android app",
		};
		descriptor.RedirectUris.Add(OwnerIdentity.MobileRedirectUri);
		AddCodeFlowPermissions(descriptor);
		return descriptor;
	}

	/// <summary>Grants the authorization code flow with PKCE, refresh tokens and revocation.</summary>
	private static void AddCodeFlowPermissions(OpenIddictApplicationDescriptor descriptor)
	{
		descriptor.Permissions.UnionWith(
		[
			Permissions.Endpoints.Authorization,
			Permissions.Endpoints.Token,
			Permissions.Endpoints.Revocation,
			Permissions.GrantTypes.AuthorizationCode,
			Permissions.GrantTypes.RefreshToken,
			Permissions.ResponseTypes.Code,
			Permissions.Prefixes.Scope + Scopes.OpenId,
			Permissions.Prefixes.Scope + Scopes.OfflineAccess,
		]);
		descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
	}

	/// <summary>Creates the client, or replaces the stored one's settings with the described ones.</summary>
	private static async Task UpsertAsync(IOpenIddictApplicationManager manager, OpenIddictApplicationDescriptor descriptor, CancellationToken cancellationToken)
	{
		var existing = await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			await manager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);
			return;
		}

		await manager.UpdateAsync(existing, descriptor, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Records that registration waited for the migration.</summary>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The database or the token service tables do not exist yet; run the backend with --migrate and start it again so the sign-in clients are registered.")]
	private partial void LogNotMigrated(Exception exception);

	/// <summary>Tells a missing database or table, which a later start repairs, from any other failure, which must stop the host.</summary>
	private static bool IsNotMigrated(Exception exception) =>
		exception is PostgresException { SqlState: UndefinedTable or UndefinedDatabase }
		|| exception.InnerException is PostgresException { SqlState: UndefinedTable or UndefinedDatabase };
	#endregion
}
