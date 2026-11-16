# ADR-0009: Authenticate the owner with external OpenID Connect providers through the API's token service

- Status: Proposed
- Date: 2026-11-13
- Deciders: Gihed Annabi
- Supersedes: the "Network protection, no login" part of
  [ADR-0004](0004-serve-both-apps-from-one-postgresql-backend-and-api.md)

## Context

ADR-0004 protected the API and the web app with no login: a loopback caller plus a `Tailscale-User-Login` header equal
to the owner. That only holds while both run on the PC behind Tailscale Serve. The owner dropped the tailnet; the API
will later run on a server and the Android app, and later an iOS app, must reach it. A header any caller can send
protects nothing there.

Forces:

- One user, but bank data: the login must be strong (MFA, passkeys) without the app storing passwords.
- Android now, iOS later: iOS apps that offer third-party sign-in must also offer Sign in with Apple, which needs a
  server-signed client secret.
- Providers differ: Google ID tokens last an hour and some flows give no refresh token; each platform would need its
  own client per provider if the apps talked to providers directly.
- The web app is a Blazor Server app: a user's token can only be read when its live connection starts, and a refreshed
  token cannot update its cookie.
- `Pivot.Framework.Containers.API` 2.1.0 supports one Keycloak issuer only.
- It must work locally now (web `https://localhost:5001`, API `http://127.0.0.1:5080`, Android emulator) and later
  deploy unchanged.

Options considered:

1. Keycloak, self-hosted, with Pivot's Keycloak integration.
1. itsme as the login.
1. The API validates the providers' ID tokens directly (several issuers, one client per platform and provider).
1. The API runs its own small token service (OpenIddict) and signs the owner in through external providers.

## Decision

We authenticate the owner through external OpenID Connect providers and let the API issue its own tokens (option 4),
with OpenIddict.

- **Token service in the API.** OpenIddict serves the authorize, token and revoke endpoints for two clients: `web`
  (confidential: authorization code and client credentials) and `mobile` (public: authorization code with PKCE).
  Access tokens last about 10 minutes; refresh tokens rotate and can be revoked. Its data lives in the existing
  PostgreSQL context.
- **Providers by configuration.** OpenIddict's client signs the owner in at the provider; a provider is enabled when
  its `Authentication:Providers:<Name>` section exists. Google first; Microsoft and Apple need configuration only.
- **One owner.** At the provider callback the provider's issuer and subject must match `Authentication:Owners`; they
  map to one stable owner id. Anyone else is refused. Email is never trusted for this.
- **API.** A fallback policy requires the owner (an owner user token, or a `web` client token) on every endpoint
  except `/health` and the sign-in endpoints. `TailnetAccessMiddleware` and the owner header are removed; the bank
  consent state is bound to the owner id.
- **Web app.** A cookie session (its own cookie name, HttpOnly, Secure, SameSite Lax) signs the owner in through the
  API with OpenID Connect; a sign-in page lists the enabled providers. A fallback policy covers pages, the Blazor hub,
  `/callback` and `/export.csv`. The web host calls the API with its own client-credentials token, because a user
  token cannot be refreshed inside a Blazor Server connection. It keeps its cross-origin and WebSocket checks.
- **Android app.** Sign-in in the system browser with PKCE (`com.banktracker.mobile://auth`); the refresh token is kept
  in secure storage and the access token refreshed before it expires. In development the emulator reaches the API at
  `http://localhost:5080` through `adb reverse`, because Google allows plain-HTTP redirects only for `localhost`.
- **Secrets.** Provider secrets, the `web` client secret, OpenIddict's signing and encryption keys and the owner
  allow-list live in user secrets or server settings, never in the repository. Signing keys persist across restarts.
- **Packages.** `OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore` and `OpenIddict.Client.WebIntegration`
  7.7.2, and `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.14, each published at least two weeks before
  pinning.

## Consequences

### Positive

- Strong login (the provider's MFA and passkeys) with no password stored by the app.
- Adding Microsoft or Apple is configuration; the apps never change per provider.
- The same model works locally and on a server.
- Tokens are short-lived, refreshable and revocable in one place.

### Negative

- A new dependency (OpenIddict) and a database migration for its tables.
- The owner depends on their provider account; losing it locks them out until the allow-list is changed.
- The `web` client secret gives owner access if it leaks.
- Apple can only be tested with an HTTPS domain; local tests cover Google.
- The emulator needs `adb reverse` once per start.
- Signing out in the web app drops its cookie and revokes the refresh token, but there is no session store: a copied
  cookie stays valid until it expires (8 hours).

## Alternatives considered

- **Keycloak** - rejected: a Java server and its database to run and patch for one user, more attack surface than the
  app itself.
- **itsme** - rejected: offering itsme sign-in requires a business partner contract.
- **Provider ID tokens validated by the API** - rejected: refresh differs per provider, Apple needs a server-signed
  secret anyway, and each platform would need its own client per provider.
