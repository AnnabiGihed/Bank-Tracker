# Run the tests

From the repository folder, run:

```powershell
dotnet test
```

Prerequisites: the Pivot.Framework feed variables (`PIVOT_PACKAGES_USER`, `PIVOT_PACKAGES_TOKEN`) and a running
Docker. The persistence, API and end-to-end tests start a real PostgreSQL in Docker through Testcontainers; without
Docker they fail with the reason and are never reported as passed.

To build and test without the Android app, which needs the Android workload, run the solution filter instead:

```powershell
dotnet test KbcTracker.Core.slnf
```

The full `dotnet test` also builds the Android app. Install the Android workload, a JDK and the Android SDK first, as
in [Run the Android app](run-the-android-app.md).

The tests use xUnit, AwesomeAssertions and NSubstitute. There is one test project per tested source project, in
the same folders under `test/`, plus end-to-end tests:

| Project | What it covers |
| --- | --- |
| `test/Core/KbcTracker.Domain.Tests` | Classification, categories, fixed-payment suggestions and statuses, month summaries, the review queue. |
| `test/Core/KbcTracker.Application.Tests` | The sync, with a fake bank. |
| `test/Infrastructure/KbcTracker.Infrastructure.Tests` | The Enable Banking client and mapping, with a fake API, and the reader of the old JSON data folder. |
| `test/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore.Tests` | The PostgreSQL mapping, the migration, the repositories and the import; each test gets its own database. |
| `test/Containers/API/KbcTracker.ApiService.Tests` | The API against PostgreSQL: reads, sync, consent, decisions, the maintenance commands, access restrictions, safe response fields, and parity with the committed OpenAPI contract. |
| `test/Containers/UI/Core/KbcTracker.Shared.Tests` | How the typed API client reads answers and refusals, with a fake backend. |
| `test/Containers/UI/Core/KbcTracker.ViewModels.Tests` | The state and computation behind each tab: filters, totals, splits, formats and backend messages. |
| `test/Containers/UI/Core/KbcTracker.Components.Tests` | The Razor components, rendered with bUnit against a stubbed backend. |
| `test/KbcTracker.E2E.Tests` | The web app and the backend started in memory together, over PostgreSQL: endpoints, access rules and the browser test below. |

`RealDataTests` reads your own JSON history in `%LOCALAPPDATA%\KbcTracker`, without changing it, and checks that
the fixed payments you accepted are still suggested. On a machine without that data it is reported as skipped.

When you change the detection, add a test and keep `RealDataTests` passing.

## Run the browser test

One Playwright test drives the web app in a real Chromium: it opens the Overview, changes a category and undoes it
with **Only this one**. It is tagged `Category=Browser`. Run only that test:

```powershell
dotnet test test/KbcTracker.E2E.Tests --filter Category=Browser
```

The first run downloads Chromium, so it needs network access. A failed run leaves a screenshot and a trace in the
`playwright-artifacts` folder next to the test assembly.
