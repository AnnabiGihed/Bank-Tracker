# KbcTracker.Web

The local web app: it serves the tabs at <https://localhost:5001> over the application layer. It is replaced by the
Android app in step 5 of [ADR-0003](../../../../../docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md).

| Path | Responsibility |
| --- | --- |
| `Program.cs` | Routes, the `POST /api` actions, the bank consent flow, and the `--sync` command-line mode. |
| `appsettings.json` | Settings; see [Configuration](../../../../../docs/reference/configuration.md). |
| `Features/<Tab>/<Tab>Page.cs` | The HTML of one tab (`Render`): Overview, FixedPayments, Transactions, Review, Categories, Settings, and the bank picker in BankConnection. |
| `Features/Shared/Pages/PageKit.cs` | What every page shares: stylesheet, page shell, formatting helpers. |
| `Features/Shared/Storage/Store.cs` | `IStore` on JSON files in the data folder. |
| `Features/Shared/Api/ApiRequest.cs` | The body of `POST /api`. |
| `Features/BankConnection/AutoSync.cs` | Syncs every `AutoSyncHours` while the app runs. |
| `Features/Categories/CategorySeed.cs` | The first-run category keywords from `appsettings.json`. |

Run it with `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`. Analyzer findings in the page classes
and `Program.cs` are warnings, not errors, until the app is retired
([ADR-0002](../../../../../docs/adr/0002-adopt-the-shared-analyzer-set.md)).
Tests: `test/Containers/UI/Hosting/KbcTracker.Web.Tests` and `test/KbcTracker.E2E.Tests`.
