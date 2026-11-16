# KbcTracker.Web

The web app: a Blazor Web App in Interactive Server mode (`prerender: false`) at <https://localhost:5001>. It
hosts the shared components of `KbcTracker.Components`
([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)), which read and change
everything through the backend's API with `BackendClient`. It stores no data and holds no bank key. Only the signed-in
owner reaches it ([ADR-0009](../../../../../docs/adr/0009-authenticate-the-owner-with-external-openid-connect-providers.md)):
the API is the token service, the web app a confidential client (`web`).

| Path | Responsibility |
| --- | --- |
| `Program.cs` | The service registrations (`AddWebAuthentication`, `AddKbcTrackerUi`, the bank launcher, Razor components), the middleware order (access, authentication, authorization, antiforgery), the endpoints and the `--sync` refusal. |
| `Features/Authentication/WebAuthenticationExtensions.cs` | Cookie session (`kbc-session`: HttpOnly, Secure, SameSite Lax, absolute 8 hours), OpenID Connect against the API (code flow with PKCE, client `web`, response by query), the fallback policy (authenticated and the `kbc_owner` claim) and the backend client with its token handler. A page navigation without a session is redirected to `/signin`; the circuit (`/_blazor`) and non-GET requests get 401 or 403. |
| `Features/Authentication/SignInEndpoints.cs`, `SignInPage.razor` | `GET /signin` (static page, no circuit, one link per provider), `GET /signin/start?provider&returnUrl` (starts the sign-in at the API with that provider; local return addresses only) and `POST /signout` (antiforgery; ends the session and revokes the refresh token at the API). All three allow anonymous callers; so do the static assets. |
| `Features/Authentication/ApiTokenClient.cs`, `ApiAccessTokenHandler.cs` | The `web` client's client-credentials token for the backend calls: cached, replaced 60 seconds before it expires, put on every request by the handler registered with `WithAccessTokenHandler`; also revokes a refresh token. |
| `Features/Authentication/OwnerAuthenticationStateProvider.cs` | Re-checks the owner claim and the session end of a live circuit every minute, so a circuit cannot outlive the cookie. |
| `Features/Authentication/WebAuthentication.cs`, `WebClientSecret.cs` | The fixed names (cookie, claims, paths, settings) and the holder of the client secret. |
| `appsettings.json` | Settings; see [Configuration](../../../../../docs/reference/configuration.md) and the table below. |
| `Features/Shared/Shell/App.razor` | The HTML shell, rendered statically: the style bundle of the components, `blazor.web.js`, Radzen's script, and the interactive `WebShell`. Reads the one-time `PageNotice` cookie and clears it. |
| `Features/Shared/Shell/WebShell.razor` | The interactive root: takes the notice, the account name and the sign-out token as parameters (a cascading value cannot cross from the static page) and cascades the notice and the account controls to the layout of `KbcTrackerRouter`. |
| `Features/Shared/Shell/AccountMenu.razor` | The signed-in account and the sign-out button (a form posting to `/signout`), which the layout shows in the shared header; the Android app supplies its own. |
| `Features/Shared/Shell/WebBankConnectionLauncher.cs` | The web's `IBankConnectionLauncher`: leaves the app for the bank's address; the bank returns to `/callback`. |
| `Features/Shared/Api/WebEndpoints.cs` | The two routes that are not components: `GET /callback` (completes the consent, returns to Settings) and `GET /export.csv` (`CsvExport` of `KbcTracker.ViewModels`). |
| `Features/Shared/Api/WebAccessMiddleware.cs` | `no-store` and `no-referrer` on every response, and the refusal of cross-site writes and WebSocket upgrades. Who may reach the app is the sign-in's job, not this middleware's. |
| `Features/Shared/Api/BackendUnavailableMiddleware.cs` | Maps a backend failure on the export or the callback to a problem response: unreachable (503), refused (403) or conflicting (409, with the backend's own reason); the wording comes from `BackendProblem` in `KbcTracker.ViewModels`. The components show the same wording themselves. |
| `Features/Shared/Api/PageNotice.cs` | Carries the message of a failed bank connection across the redirect to Settings, where it is shown once. |

## Sign-in settings

| Setting | Meaning |
| --- | --- |
| `Authentication:Clients:Web:Secret` | Required. The secret of the API's `web` client; the AppHost passes it to both hosts (outside it, set it as an environment variable, `Authentication__Clients__Web__Secret`, or in user secrets). Never commit it. |
| `Authentication:Authority` | Optional. The address the browser reaches the API at, which the sign-in uses; `Backend:BaseUrl` when absent. Google only accepts `http://localhost:5080/...` as the API's callback, so the AppHost sets it to `localhost` while `Backend:BaseUrl` stays on `127.0.0.1` (the API listens on IPv4 only, and `localhost` may try IPv6 first). |
| `SignIn:Providers` | The providers the sign-in page offers (`["Google"]` by default). The list lives here because the API has no provider list to ask for; the API refuses a provider it has not configured. |

The metadata is fetched over HTTP only when the authority is a loopback address in Development; everywhere else HTTPS is required.
The sign-in tests use a test-only route in `test/KbcTracker.E2E.Tests` that issues the session cookie; the product has no bypass.

Start the backend first, then run `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`. The analyzers
run with warnings as errors, as in every project
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
Tests: `test/KbcTracker.E2E.Tests`.
