using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Registers the owner's sign-in (ADR-0009): the token service (authorize, token and revoke endpoints with
///              the authorization code flow and PKCE, rotating refresh tokens and client credentials), its client toward
///              the external providers, the validation of the API's own tokens, and the fallback "Owner" policy.
///
///              The signing and encryption keys must survive a restart, or every issued token stops working: outside
///              Development they are the certificate files named in <c>Authentication:Signing</c> and
///              <c>Authentication:Encryption</c>; in Development, without those, OpenIddict's development certificates in
///              the user's certificate store.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
	#region Constants
	/// <summary>The shortest sensible access token lifetime, which keeps a misconfigured zero from issuing expired tokens.</summary>
	private const int MinimumAccessTokenSeconds = 1;
	#endregion

	#region Public Methods
	/// <summary>Registers authentication, the token service, the provider client and the owner policy.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration holding the <c>Authentication</c> section.</param>
	/// <param name="environment">The host environment, which decides where the keys come from.</param>
	/// <returns>The same service collection.</returns>
	/// <exception cref="InvalidOperationException">The section is invalid, or a key is missing outside Development.</exception>
	public static IServiceCollection AddOwnerAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(environment);
		var options = configuration.GetSection(OwnerSignInOptions.SectionName).Get<OwnerSignInOptions>() ?? new();
		if (options.AccessTokenSeconds < MinimumAccessTokenSeconds)
		{
			throw new InvalidOperationException("Authentication:AccessTokenSeconds must be at least 1.");
		}

		var keys = Keys.Load(options, environment);
		services.AddSingleton(options);
		services.AddSingleton(new OwnerAllowList(options.Owners));
		services.AddSingleton<OpenIddictClientSeeder>();
		services.AddHostedService(provider => provider.GetRequiredService<OpenIddictClientSeeder>());
		AddOwnerAuthorization(services);
		var openIddict = services.AddOpenIddict();
		openIddict.AddCore(core => core.UseEntityFrameworkCore().UseDbContext<KbcTrackerDbContext>());
		AddTokenService(openIddict, options, keys);
		openIddict.AddValidation(validation =>
		{
			validation.UseLocalServer();
			validation.UseAspNetCore();
			validation.AddAudiences(OwnerIdentity.Audience);
		});
		if (options.Providers.Count > 0)
		{
			AddProviderClient(openIddict, options, keys);
		}

		return services;
	}
	#endregion

	#region Private Helpers
	/// <summary>Registers the short-lived sign-in cookie, the validation scheme and the fallback "Owner" policy.</summary>
	private static void AddOwnerAuthorization(IServiceCollection services)
	{
		// The cookie lives between the provider's callback and the authorize endpoint only; the API itself is called with
		// bearer tokens. The listener is plain HTTP on loopback, so the cookie cannot be Secure-only.
		services.AddAuthentication(authentication => authentication.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
			.AddCookie(OwnerIdentity.SignInCookie, cookie =>
			{
				cookie.Cookie.Name = OwnerIdentity.SignInCookie;
				cookie.Cookie.HttpOnly = true;
				cookie.Cookie.SameSite = SameSiteMode.Lax;
				cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
				cookie.ExpireTimeSpan = TimeSpan.FromMinutes(10);
			});
		var owner = new AuthorizationPolicyBuilder(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
			.RequireAuthenticatedUser()
			.RequireClaim(OwnerIdentity.ClaimType, OwnerIdentity.Id)
			.Build();
		services.AddAuthorizationBuilder()
			.AddPolicy(OwnerIdentity.PolicyName, owner)
			.SetFallbackPolicy(owner);
	}

	/// <summary>Registers the authorize, token and revoke endpoints and their flows.</summary>
	private static void AddTokenService(OpenIddictBuilder openIddict, OwnerSignInOptions options, Keys keys) =>
		openIddict.AddServer(server =>
		{
			server.SetAuthorizationEndpointUris("connect/authorize")
				.SetTokenEndpointUris("connect/token")
				.SetRevocationEndpointUris("connect/revoke")
				.AllowAuthorizationCodeFlow()
				.RequireProofKeyForCodeExchange()
				.AllowRefreshTokenFlow()
				.AllowClientCredentialsFlow()
				.RegisterScopes(Scopes.OpenId, Scopes.OfflineAccess)
				.SetAccessTokenLifetime(TimeSpan.FromSeconds(options.AccessTokenSeconds))

				// A redeemed refresh token is dead at once: one owner, so no concurrent redemption needs a grace period, and
				// a replayed token is a sign of theft.
				.SetRefreshTokenReuseLeeway(TimeSpan.Zero);
			keys.AddTo(server);

			// The listener is plain HTTP on loopback by design (ADR-0004); a TLS-terminating proxy comes with the VPS.
			server.UseAspNetCore()
				.EnableAuthorizationEndpointPassthrough()
				.EnableTokenEndpointPassthrough()
				.DisableTransportSecurityRequirement();
		});

	/// <summary>Registers the client that signs the owner in at the configured external providers.</summary>
	private static void AddProviderClient(OpenIddictBuilder openIddict, OwnerSignInOptions options, Keys keys) =>
		openIddict.AddClient(client =>
		{
			client.AllowAuthorizationCodeFlow();
			keys.AddTo(client);
			client.UseAspNetCore()
				.EnableRedirectionEndpointPassthrough()
				.DisableTransportSecurityRequirement();
			client.UseSystemNetHttp();
			client.UseWebProviders(web => SignInProviders.Register(web, options.Providers));
		});
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Holds the certificates that sign and encrypt tokens, or says to use the development ones.
	/// </summary>
	/// <param name="Signing">The signing certificate, or <see langword="null"/> for the development one.</param>
	/// <param name="Encryption">The encryption certificate, or <see langword="null"/> for the development one.</param>
	private sealed record Keys(X509Certificate2? Signing, X509Certificate2? Encryption)
	{
		#region Public Methods
		/// <summary>Reads the configured certificate files.</summary>
		/// <param name="options">The configured paths.</param>
		/// <param name="environment">The host environment.</param>
		/// <returns>The keys to use.</returns>
		/// <exception cref="InvalidOperationException">A key is missing outside Development, or a file cannot be read.</exception>
		public static Keys Load(OwnerSignInOptions options, IHostEnvironment environment)
		{
			var signing = Read(options.Signing, "Signing");
			var encryption = Read(options.Encryption, "Encryption");
			if (!environment.IsDevelopment() && (signing is null || encryption is null))
			{
				throw new InvalidOperationException(
					"Configure Authentication:Signing:Path and Authentication:Encryption:Path (PKCS#12 files kept outside the repository): the token keys must survive restarts.");
			}

			return new Keys(signing, encryption);
		}

		/// <summary>Adds the keys to the token service.</summary>
		/// <param name="server">The server builder.</param>
		public void AddTo(OpenIddictServerBuilder server)
		{
			if (Signing is null || Encryption is null)
			{
				server.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
				return;
			}

			server.AddSigningCertificate(Signing).AddEncryptionCertificate(Encryption);
		}

		/// <summary>Adds the keys to the provider client, which protects its state with them.</summary>
		/// <param name="client">The client builder.</param>
		public void AddTo(OpenIddictClientBuilder client)
		{
			if (Signing is null || Encryption is null)
			{
				client.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
				return;
			}

			client.AddSigningCertificate(Signing).AddEncryptionCertificate(Encryption);
		}
		#endregion

		#region Private Helpers
		/// <summary>Loads one PKCS#12 file when a path is configured.</summary>
		private static X509Certificate2? Read(CertificateFileOptions file, string name)
		{
			if (string.IsNullOrWhiteSpace(file.Path))
			{
				return null;
			}

			try
			{
				return X509CertificateLoader.LoadPkcs12FromFile(file.Path, string.IsNullOrEmpty(file.Password) ? null : file.Password);
			}
			catch (Exception exception) when (exception is IOException or System.Security.Cryptography.CryptographicException)
			{
				throw new InvalidOperationException($"Authentication:{name}:Path does not point at a readable PKCS#12 file.", exception);
			}
		}
		#endregion
	}
	#endregion
}
