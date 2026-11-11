# Run the tests

From the repository folder, run:

```powershell
dotnet test
```

Prerequisites: the Pivot.Framework feed variables (`PIVOT_PACKAGES_USER`, `PIVOT_PACKAGES_TOKEN`) and a running
Docker. The persistence, API and end-to-end tests start a real PostgreSQL in Docker through Testcontainers; without
Docker they fail with the reason and are never reported as passed.

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
| `test/KbcTracker.E2E.Tests` | The web app and the backend started in memory together, over PostgreSQL. |

`RealDataTests` reads your own JSON history in `%LOCALAPPDATA%\KbcTracker`, without changing it, and checks that
the fixed payments you accepted are still suggested. On a machine without that data it is reported as skipped.

When you change the detection, add a test and keep `RealDataTests` passing.
