# KbcTracker.ViewModels

The state and computation behind every tab ([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)).
Plain .NET: no Razor and no ASP.NET Core, so the web app and the Android app share it. A view model is built from the
loaded analysis and returns plain data (text already formatted, status, colour hints, requests to send); a component
only draws it. The logic moved here from the old page classes of `KbcTracker.Web` without being rewritten.

| Path | Responsibility |
| --- | --- |
| `Features/Shared/WorkspaceStore.cs` | Loads the workspace once through `BackendClient`, rebuilds the analysis and the display session, keeps the selected month (an unknown month falls back to the newest), reloads after a decision or a sync, and raises `Changed`. |
| `Features/Shared/Formats.cs` | Amounts (Belgian French), `dd/MM` dates and English month names, with the culture given explicitly. |
| `Features/Shared/BackendProblem.cs`, `BackendResult.cs`, `BackendCall.cs` | Turn a failed call into the message to show: access refused (403), conflict with the backend's own reason (409), unavailable (503), or a refused action with its safe message. |
| `Features/Shared/DecisionRequests.cs` | The `DecisionRequest` of every action (`confirm`, `payeeCategory`, `txnOnly`, `accept`, `dismiss`, `mark`, `removeFixed`, `updateFixed`, `saveCategory`, `deleteCategory`, `split`, `unsplit`, `rename`). |
| `Features/Shared/ShellViewModel.cs` | The frame around every tab: sync status, month selector, tabs with badges, sync banner. |
| `Features/Shared/CategoryOptions.cs` | Category pickers: tree order, grouped by type, indented by level. |
| `Features/Shared/CsvExport.cs` | The CSV export, moved from the web host. |
| `Features/Shared/IBankConnectionLauncher.cs`, `IExportSaver.cs` | Ports a host implements: open the bank's consent page, hand over an exported file. |
| `Features/Overview/` | `OverviewViewModel`: notice (`AttentionNotice`, with `WaitingSuggestions` and one `SuggestionLink` per tab that has suggestions), five key figures, the last twelve months, `CategoryDrillDown` (doughnut data, path), fixed payments, biggest expenses. |
| `Features/Transactions/` | `TransactionsViewModel` (filters, rows, totals), `SplitEditor` (2 to 100 parts, amounts to the cent, adding up exactly). |
| `Features/FixedPayments/` | `FixedPaymentsViewModel` (suggestions, month totals, rows with status; subscriptions left out), `FixedPaymentEdit`. |
| `Features/Income/` | `IncomeViewModel` (earnings, refunds and loan received sections from `Analysis.Behind`, earnings of 12 months, earnings by payer), `MonthFigureNames` (the `show` query words). |
| `Features/Subscriptions/` | `SubscriptionsViewModel` (suggestions, rows with cost per month and year, last and next charge, totals). |
| `Features/Review/` | `ReviewViewModel` (direction filter, confirm all shown). |
| `Features/Categories/` | `CategoriesViewModel` (tree per type with roll-up totals, bars, comparison, expanded rows), `CategoryEdit` (the create, move, edit and delete form). |
| `Features/Settings/` | `SettingsViewModel` (connection status, history). |
| `Features/BankConnection/` | `BankPickerViewModel` (banks with KBC first, starts the consent). |

Tests: `test/Containers/UI/Core/KbcTracker.ViewModels.Tests`.
