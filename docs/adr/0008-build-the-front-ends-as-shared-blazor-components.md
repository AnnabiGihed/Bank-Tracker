# ADR-0008: Build the web and Android front ends from shared Blazor components

- Status: Proposed
- Date: 2026-11-12
- Deciders: Gihed Annabi
- Amends: [ADR-0004](0004-serve-both-apps-from-one-postgresql-backend-and-api.md) (the shape of the clients),
  [ADR-0002](0002-adopt-the-shared-analyzer-set.md) (packages)

## Context

ADR-0004 made the web app a client of the API and planned an Android app as a second client. The web app's pages are
hand-written HTML built in C# strings (`PageKit` and one `…Page` class per tab), with Chart.js loaded from a CDN, which
fails offline. Nothing of them can be reused on a phone. The owner wants both clients and one UI for both.

Forces:

- One developer, one user: two separate UIs would double every screen change.
- The pages turn the API's workspace back into the Domain `Analysis` (`WorkspaceSnapshot.ToAnalysis()`) and compute
  month summaries, fixed-payment status and category roll-ups from it. That logic is tested and must not be rewritten.
- `Pivot.Framework.Application` 2.1.0 and `Pivot.Framework.Domain` depend only on plain `net10.0` libraries
  (FluentValidation, CSharpFunctionalExtensions, Microsoft.Extensions abstractions), so a phone app can reference them.
- The API stays on loopback with no authentication until it moves to a VPS (a later ADR); the bank's consent redirect is
  fixed at `https://localhost:5001/callback`.
- Every project builds with the shared analyzers and warnings as errors (ADR-0002).
- The MAUI workloads (`android`, `maui-windows`) and the Android SDK are installed on the PC.

Options considered:

1. Keep the HTML pages for the web, and write the Android app with native MAUI XAML pages.
1. Blazor components in a Razor class library, hosted by a Blazor Web App and by a MAUI Blazor Hybrid app, with the
   clients still rehydrating the Domain `Analysis`.
1. The same components, with new API read endpoints that return per-tab view data instead of the whole workspace.
1. A Blazor WebAssembly web app calling the API from the browser.

## Decision

We build both front ends from one set of Blazor components (option 2), with the Radzen.Blazor component library.

- **Projects** (Pivot's canonical UI layout):
  - `src/Containers/UI/Core/KbcTracker.Shared` keeps `BackendClient` and gains its registration (`AddBackendClient`).
  - `src/Containers/UI/Core/KbcTracker.ViewModels` holds the workspace store, one view model per tab, the formatting
    (euro amounts in fr-BE, dates as `dd/MM/yyyy`, set explicitly rather than taken from the host's culture), the CSV
    export and the mapping of backend refusals to messages. It has no Razor and no ASP.NET Core dependency.
  - `src/Containers/UI/Core/KbcTracker.Components` holds the layout, the tabs and the dialogs as `.razor` markup with a
    `.razor.cs` code-behind each; formatting and logic stay in the view models.
  - `src/Containers/UI/Hosting/KbcTracker.Web` becomes a Blazor Web App in Interactive Server mode, without
    prerendering. It keeps its name, `https://localhost:5001`, its access middleware and the endpoints the browser needs
    without a component: `/callback` (the bank's redirect) and `/export.csv`.
  - `src/Containers/UI/Hosting/KbcTracker.Mobile` is a MAUI Blazor Hybrid app for Android only
    (`com.BankTracker.mobile`, minimum API 29), part of `KbcTracker.slnx`.
- **Computation** stays on the client: the store loads the workspace once, rehydrates `Analysis` and refreshes after each
  decision or sync; the view models call the same Domain methods the pages called. The API contract does not change.
- **Radzen.Blazor 12.1.8** provides the components, dialogs, notifications and charts; its assets ship in the package,
  so Chart.js and the CDN go away.
- **The web app** calls the API server-side through `BackendClient`, as before, so the API stays on loopback, needs no
  CORS and the owner header never reaches a browser.
- **The Android app** calls the API directly. In development it reaches the PC's API from the emulator at
  `http://10.0.2.2:5080/`, which arrives on the PC's loopback; cleartext HTTP is allowed for that address in Debug only.
  It has no production backend until the API moves to the VPS with authentication.
- **The bank connection** (connect, reconnect and the callback) stays in the web app; the Android app shows the
  connection status and says to reconnect from the web app.
- **Packages**, pinned centrally, each published at least two weeks before it was pinned: `Radzen.Blazor` 12.1.8,
  `Microsoft.Maui.Controls` and `Microsoft.AspNetCore.Components.WebView.Maui` 10.0.120, `Microsoft.Extensions.Http`
  and `Microsoft.AspNetCore.Components.Web` 10.0.14, `bunit` 2.13.3 (component tests) and `Microsoft.Playwright`
  1.64.1 (one browser smoke test of the web app).
- **Tests:** view models get unit tests, components get bUnit tests, the web host's tests check its endpoints and access
  rules instead of HTML text, and one Playwright test drives the web app in a real browser. The Android app is checked by
  hand in the emulator.

## Consequences

### Positive

- One UI for the web and the phone; a screen changes in one place.
- The Domain logic the pages used is reused unchanged, and the API contract does not move.
- No CDN: the web app works offline on the PC.
- Components and view models get real unit and component tests instead of assertions on HTML strings.

### Negative

- Every full build and `verify.py` also builds the Android app: slower, and it needs the Android workload, a JDK and the
  Android SDK on the build machine.
- The phone downloads the whole workspace and the Domain assemblies; read endpoints (option 3) will be needed when the
  API is remote and the data grows.
- Interactive Server keeps a live connection per open tab; a dropped connection reloads the workspace.
- A third-party component library: Radzen's API changes between major versions and is followed deliberately.
- Until the VPS and authentication exist, the Android app only works in the emulator against the PC.

## Alternatives considered

- **HTML pages plus native XAML pages** - rejected: two UIs to keep in step, and the web keeps its CDN charts.
- **New API read endpoints now** - rejected for this step: it changes the contract and the API together with the whole
  UI; it is the next step once the API is remote.
- **Blazor WebAssembly** - rejected: the browser would call the API directly, which needs CORS and browser-facing access
  rules that belong with the VPS and authentication decision.
