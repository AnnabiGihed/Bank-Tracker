# KbcTracker.Mobile

The Android app: a .NET MAUI Blazor Hybrid shell (`net10.0-android`, application id `com.BankTracker.mobile`, Android 10
and later) that hosts the shared components of `KbcTracker.Components`
([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)). It reads and changes
everything through the backend's API with `BackendClient`, stores no data and holds no bank key. The owner signs in
through the system browser with PKCE ([ADR-0009](../../../../../docs/adr/0009-authenticate-the-owner-with-external-openid-connect-providers.md));
the only secret the app keeps is the refresh token, in the platform's secure storage. It does not reference
the service defaults of the PC hosts. The CSV export stays web-only, and connecting or reconnecting the bank happens in
the web app on the PC.

| Path | Responsibility |
| --- | --- |
| `MauiProgram.cs` | Builds the app: Blazor web view, the embedded settings, the sign-in services, `AddBackendClient` with `AccessTokenHandler`, `AddKbcTrackerUi` (singleton store, no CSV path) and the launcher. |
| `appsettings.json` | Embedded settings; `Backend:BaseUrl` is empty. A Release build therefore has no backend address and stops at startup until one is configured (with the VPS work); until then only Debug builds run. `SignIn:Providers` lists the providers the sign-in screen offers (`Google`). |
| `appsettings.Development.json` | Embedded in Debug builds only, and what makes a Debug build run: `Backend:BaseUrl` is `http://localhost:5080/`, which `adb reverse tcp:5080 tcp:5080` tunnels to the PC's backend (Google allows a plain-HTTP redirect only for `localhost`). |
| `Features/Authentication/MobileSession.cs` | The sign-in state (starting, signed out, signed in): restores a stored session, signs in with `WebAuthenticator` (system browser, `com.banktracker.mobile://auth`, PKCE, state check), signs out. |
| `Features/Authentication/AccessTokenHandler.cs` | The `DelegatingHandler` that puts the access token on every backend call; it never retries a 401. |
| `Features/Authentication/SecureStorageRefreshTokenStore.cs` | `IRefreshTokenStore` over `SecureStorage` (the Android Keystore). |
| `Features/Authentication/SignInScreen.razor`, `MobileAccountMenu.razor` | The screen shown while nobody is signed in (one button per provider), and the sign-out button handed to the shared header as the cascading `AccountMenu` fragment. |
| `Features/Authentication/MobileSignInOptions.cs`, `SessionState.cs` | The providers to offer, and the sign-in states. |
| `Features/Shared/Shell/App.cs` | The MAUI application and its one window. |
| `Features/Shared/Shell/MainPage.cs` | A `ContentPage` holding the `BlazorWebView` (host page `wwwroot/index.html`), built in code; mounts `MobileShell` at `#app`. |
| `Features/Shared/Shell/MobileShell.razor` | The root component: the sign-in screen while nobody is signed in, otherwise the shared `KbcTrackerRouter` with the sign-out button as `AccountMenu`, and no one-time notice. |
| `Features/Shared/Shell/MobileBankConnectionLauncher.cs` | `IBankConnectionLauncher` that cannot launch and says "Connect or reconnect the bank from the web app on your PC." |
| `wwwroot/index.html` | The host page: the components' style bundle, Radzen's script, `blazor.webview.js`, `#app` and the error bar. |
| `Platforms/Android/` | `MainActivity`, `MainApplication` (its `[Application]` registration in `MainApplication.Registration.cs`), `WebAuthenticationCallbackActivity` (claims the redirect `com.banktracker.mobile://auth`), `AndroidManifest.xml` (INTERNET permission, the Custom Tabs `<queries>` entry) and `Resources/xml/network_security_config.xml`. |
| `Resources/` | The app icon and splash screen (simple SVGs). |

Cleartext HTTP is allowed only in Debug builds and only to `localhost` and `127.0.0.1`: the Debug registration in
`MainApplication.Registration.cs` references the network security config, the Release one does not, so a Release
build has no cleartext exception.

## Run in the emulator

1. Start the backend on the PC: the AppHost (`dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost`)
   or the API service alone (it listens on `http://127.0.0.1:5080`).
2. Start an Android emulator (API 29 or later) and run `adb reverse tcp:5080 tcp:5080` (again after each emulator start).
3. Run `dotnet build src/Containers/UI/Hosting/KbcTracker.Mobile -t:Run -f net10.0-android`.
4. Tap "Sign in with Google" and sign in with the owner's account in the browser.

The app reaches the PC's API at `localhost:5080` through the reverse tunnel. Building needs the Android workload, a JDK and the Android SDK. For
builds and tests without Android use the solution filter `KbcTracker.Core.slnf` (every project except this one).
The analyzers run with warnings as errors, as in every project
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
