# KbcTracker.Mobile

The Android app: a .NET MAUI Blazor Hybrid shell (`net10.0-android`, application id `com.BankTracker.mobile`, Android 10
and later) that hosts the shared components of `KbcTracker.Components`
([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)). It reads and changes
everything through the backend's API with `BackendClient`, stores no data and holds no bank key. It does not reference
the service defaults of the PC hosts. The CSV export stays web-only, and connecting or reconnecting the bank happens in
the web app on the PC.

| Path | Responsibility |
| --- | --- |
| `MauiProgram.cs` | Builds the app: Blazor web view, the embedded settings, `AddBackendClient`, `AddKbcTrackerUi` (singleton store, no CSV path) and the launcher. |
| `appsettings.json` | Embedded settings; `Backend:BaseUrl` is empty. A Release build therefore has no backend address and stops at startup until one is configured (with the VPS work); until then only Debug builds run. |
| `appsettings.Development.json` | Embedded in Debug builds only, and what makes a Debug build run: `Backend:BaseUrl` is `http://10.0.2.2:5080/`, the PC's loopback as the emulator sees it. No owner login. |
| `Features/Shared/Shell/App.cs` | The MAUI application and its one window. |
| `Features/Shared/Shell/MainPage.cs` | A `ContentPage` holding the `BlazorWebView` (host page `wwwroot/index.html`), built in code; mounts `MobileShell` at `#app`. |
| `Features/Shared/Shell/MobileShell.razor` | The root component: renders the shared `KbcTrackerRouter`, with no one-time notice. |
| `Features/Shared/Shell/MobileBankConnectionLauncher.cs` | `IBankConnectionLauncher` that cannot launch and says "Connect or reconnect the bank from the web app on your PC." |
| `wwwroot/index.html` | The host page: the components' style bundle, Radzen's script, `blazor.webview.js`, `#app` and the error bar. |
| `Platforms/Android/` | `MainActivity`, `MainApplication` (its `[Application]` registration in `MainApplication.Registration.cs`), `AndroidManifest.xml` (INTERNET permission) and `Resources/xml/network_security_config.xml`. |
| `Resources/` | The app icon and splash screen (simple SVGs). |

Cleartext HTTP is allowed only in Debug builds and only to `10.0.2.2`: the Debug registration in
`MainApplication.Registration.cs` references the network security config, the Release one does not, so a Release
build has no cleartext exception.

## Run in the emulator

1. Start the backend on the PC: the AppHost (`dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost`)
   or the API service alone (it listens on `http://127.0.0.1:5080`).
2. Start an Android emulator (API 29 or later).
3. Run `dotnet build src/Containers/UI/Hosting/KbcTracker.Mobile -t:Run -f net10.0-android`.

The app reaches the PC's API at `10.0.2.2:5080`. Building needs the Android workload, a JDK and the Android SDK. For
builds and tests without Android use the solution filter `KbcTracker.Core.slnf` (every project except this one).
The analyzers run with warnings as errors, as in every project
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
