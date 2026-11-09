# Run the tests

From the repository folder, run:

```powershell
dotnet test
```

The tests use xUnit, AwesomeAssertions and NSubstitute. There is one test project per source project, in the same
folders under `test/`, plus end-to-end tests:

| Project | What it covers |
| --- | --- |
| `test/Core/KbcTracker.Domain.Tests` | Classification, categories, fixed-payment suggestions and statuses, month summaries, the review queue. |
| `test/Core/KbcTracker.Application.Tests` | The sync, with a fake bank. |
| `test/Infrastructure/KbcTracker.Infrastructure.Tests` | The Enable Banking client and mapping, with a fake API. |
| `test/Containers/UI/Hosting/KbcTracker.Web.Tests` | The JSON storage, in a temporary folder. |
| `test/KbcTracker.E2E.Tests` | The web app started in memory, over a temporary data folder. |

`RealDataTests` reads your own history in `%LOCALAPPDATA%\KbcTracker`, without changing it, and checks that the
fixed payments you accepted are still suggested. On a machine without that data it is reported as skipped.

When you change the detection, add a test and keep `RealDataTests` passing.
