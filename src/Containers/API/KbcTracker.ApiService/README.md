# KbcTracker.ApiService

The backend. It serves the API the user interfaces use (history, categories, the workspace, decisions, bank
consent, connection status and sync), runs the automatic sync, and offers the maintenance commands `--migrate`,
`--import-json`, `--initialize` and `--sync`. It is also the token service of the owner's sign-in
([ADR-0009](../../../../docs/adr/0009-authenticate-the-owner-with-external-openid-connect-providers.md)): every endpoint
except `/health` and the sign-in endpoints needs the owner's access token.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | Configuration, the loopback listener and the request pipeline. |
| `Features/Shared/BackendController.cs` | History, categories, connection status, manual sync and health. |
| `Features/Workspace/WorkspaceController.cs` | The workspace and the decisions. |
| `Features/BankConnection/BankConsentController.cs` | The bank list and the consent flow. |
| `Features/BankConnection/AutomaticBankSync.cs` | Syncs every `AutoSyncHours` while the backend runs. |
| `Features/Shared/BackendAdministration.cs` | The maintenance commands. |
| `Features/Authentication/AuthenticationServiceCollectionExtensions.cs` | Registers the token service (authorize, token and revoke endpoints), the provider client, token validation and the fallback "Owner" policy; loads the token keys. |
| `Features/Authentication/OAuthController.cs` | The authorize and token endpoints: authorization code with PKCE, refresh with rotation, client credentials. |
| `Features/Authentication/ProviderCallbackController.cs` | `/callback/login/<provider>`: checks the provider account against the allow-list, refuses anyone else. |
| `Features/Authentication/OwnerRefusalLog.cs` | The warning for a refused provider account: issuer and subject to add to `Authentication:Owners`, never the email. |
| `Features/Authentication/OwnerAllowList.cs` | Maps an issuer and subject from `Authentication:Owners` to the one owner id. |
| `Features/Authentication/OpenIddictClientSeeder.cs` | Registers the `web` (confidential) and `mobile` (public) clients at start. |
| `Features/Authentication/SignInProviders.cs` | The supported providers (Google, Microsoft); one is on when `Authentication:Providers:<Name>` exists. |
| `Features/Authentication/OwnerSignInOptions.cs` | The `Authentication` section, with `AllowedOwner`, `ProviderOptions`, `ClientsOptions`, `WebClientOptions` and `CertificateFileOptions`. |
| `Features/Authentication/OwnerIdentity.cs` | The owner id, the owner claim, the policy name, the audience and the client ids. |
| `Features/Authentication/BearerSecurityOperationFilter.cs` | Marks protected operations with the bearer scheme in the OpenAPI document. |

## Sign-in configuration

Settings live under `Authentication` in user secrets (Development) or server settings, never in the repository:

| Setting | Meaning |
| --- | --- |
| `Authentication:Clients:Web:Secret` | Required. The `web` client's secret; the AppHost generates and passes it. |
| `Authentication:Clients:Web:ExtraRedirectUris` | Optional list of redirect addresses besides `https://localhost:5001/signin-oidc`. |
| `Authentication:Providers:Google:ClientId`, `ClientSecret` | Enables Google sign-in; `Microsoft` works the same (plus an optional `Tenant`). |
| `Authentication:Owners` | List of `{ Issuer, Subject }` provider accounts that may sign in; anyone else is refused. |
| `Authentication:Signing:Path`, `Password`, `Authentication:Encryption:Path`, `Password` | PKCS#12 files that sign and encrypt tokens. Required outside Development. |
| `Authentication:AccessTokenSeconds` | Access token lifetime, 600 by default. |

The token keys must persist: a key that changes at a restart invalidates every issued token. Outside Development the host
refuses to start without the two certificate files; in Development, without them, OpenIddict's development certificates
in the user's certificate store are used, which persist as well. The clients are registered at start and need the database
migrated first (`--migrate`); a start on an unmigrated database logs a warning and skips the registration.

See [Run the backend](../../../../docs/how-to/run-the-backend.md) for configuration, setup order and access
restrictions, and the [OpenAPI contract](../../../../docs/api/openapi.json) for the endpoints.
Tests: `test/Containers/API/KbcTracker.ApiService.Tests`.
