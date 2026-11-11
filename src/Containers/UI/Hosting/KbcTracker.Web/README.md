# KbcTracker.Web

The local web app: it serves the tabs at <https://localhost:5001> and reads and changes everything through the
backend's API with `BackendClient`. It stores no data and holds no bank key.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | The backend client, the middleware and the routes. |
| `appsettings.json` | Settings; see [Configuration](../../../../../docs/reference/configuration.md). |
| `Features/Shared/Api/WebEndpoints.cs` | The routes: pages, `POST /api` decisions, the bank consent and callback, sync, CSV export. |
| `Features/Shared/Api/WebAccessMiddleware.cs` | The loopback and owner-identity gate. |
| `Features/Shared/Api/BackendUnavailableMiddleware.cs` | Shows that the backend is unreachable instead of failing. |
| `Features/Shared/Api/CsvExport.cs` | The CSV export of the transactions. |
| `Features/<Tab>/<Tab>Page.cs` | The HTML of one tab (`Render`): Overview, FixedPayments, Transactions, Review, Categories, Settings, and the bank picker in BankConnection. |
| `Features/Shared/Pages/PageKit.cs` | What every page shares: stylesheet, page shell, formatting helpers. |

Start the backend first, then run `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`. Analyzer
findings in the page classes and `Program.cs` are warnings, not errors, for now
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
Tests: `test/KbcTracker.E2E.Tests`.
