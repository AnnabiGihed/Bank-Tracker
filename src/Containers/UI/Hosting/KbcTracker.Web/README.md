# KbcTracker.Web

The local web app: a Blazor Web App in Interactive Server mode (`prerender: false`) at <https://localhost:5001>. It
hosts the shared components of `KbcTracker.Components`
([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)), which read and change
everything through the backend's API with `BackendClient`. It stores no data and holds no bank key.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | The service registrations (`AddBackendClient`, `AddKbcTrackerUi`, the bank launcher, Razor components), the middleware order (access first, so it covers `/_blazor` and the static assets), the endpoints and the `--sync` refusal. |
| `appsettings.json` | Settings; see [Configuration](../../../../../docs/reference/configuration.md). |
| `Features/Shared/Shell/App.razor` | The HTML shell, rendered statically: the style bundle of the components, `blazor.web.js`, Radzen's script, and the interactive `WebShell`. Reads the one-time `PageNotice` cookie and clears it. |
| `Features/Shared/Shell/WebShell.razor` | The interactive root: takes the notice as a parameter (a cascading value cannot cross from the static page) and cascades it to the layout of `KbcTrackerRouter`. |
| `Features/Shared/Shell/WebBankConnectionLauncher.cs` | The web's `IBankConnectionLauncher`: leaves the app for the bank's address; the bank returns to `/callback`. |
| `Features/Shared/Api/WebEndpoints.cs` | The two routes that are not components: `GET /callback` (completes the consent, returns to Settings) and `GET /export.csv` (`CsvExport` of `KbcTracker.ViewModels`). |
| `Features/Shared/Api/WebAccessMiddleware.cs` | The loopback and owner-identity gate, and the refusal of cross-site writes. |
| `Features/Shared/Api/BackendUnavailableMiddleware.cs` | Maps a backend failure on the export or the callback to a problem response: unreachable (503), refused (403) or conflicting (409, with the backend's own reason); the wording comes from `BackendProblem` in `KbcTracker.ViewModels`. The components show the same wording themselves. |
| `Features/Shared/Api/PageNotice.cs` | Carries the message of a failed bank connection across the redirect to Settings, where it is shown once. |

Start the backend first, then run `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`. The analyzers
run with warnings as errors, as in every project
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
Tests: `test/KbcTracker.E2E.Tests`.
