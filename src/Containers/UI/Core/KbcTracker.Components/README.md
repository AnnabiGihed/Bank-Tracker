# KbcTracker.Components

The Razor components of both front ends ([ADR-0008](../../../../../docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md)),
built with Radzen.Blazor on top of the view models in `KbcTracker.ViewModels`. The web app (Interactive Server) and the
Android app (MAUI Blazor Hybrid) both use them. Every component is a `.razor` markup file with a `.razor.cs` code-behind
(no `@code` blocks) and, where it needs one, an isolated `.razor.css` that takes its colours from CSS custom properties
set in `MainLayout.razor.css`. All text formatting comes from the view models and `Formats`.

A host calls `AddKbcTrackerUi(ServiceLifetime.Scoped | Singleton, options)`, registers `BackendClient`
(`AddBackendClient`) and its own `IBankConnectionLauncher`, renders `KbcTrackerRouter`, and links the project's scoped
style bundle (`_content/KbcTracker.Components/KbcTracker.Components.bundle.scp.css`) and Radzen's script
(`_content/Radzen.Blazor/Radzen.Blazor.js`).

| Path | Responsibility |
| --- | --- |
| `Features/Shared/KbcTrackerUiRegistration.cs` | `AddKbcTrackerUi`: Radzen services, the `WorkspaceStore` (scoped for the web circuit, singleton for the app; never transient) and the host's options. The view models are not registered: each page builds its own from the loaded analysis. |
| `Features/Shared/KbcTrackerUiOptions.cs` | What a host tells the components: `ExportCsvPath`, the address the CSV export is downloaded from. Settings shows the link only when it is set (web app). |
| `Features/Shared/TabRoutes.cs` | Addresses of the tabs with the month in `?m=yyyy-MM`, and of Transactions filtered on a category. |
| `Features/Shared/Layout/MainLayout` | Radzen host components and the Material theme, header, tab bar, sync banner, backend status and an `ErrorBoundary` around the page. Reads `m` from the address and gives it to the store; shows a one-time `PageNotice` (cascading value) a host passes in. |
| `Features/Shared/Layout/ShellHeader`, `TabBar`, `SyncBanner`, `BackendStatus` | The parts of the frame: sync status and month selector, tabs with badges, failed or partial sync, and the `BackendProblem` text with "Try again". |
| `Features/Shared/Layout/KbcTrackerRouter`, `HomePage` | The router both hosts render, and `/` which opens `/overview`. |
| `Features/Shared/Layout/TabPageBase.cs` | Base of the tab pages: loads the store once, rebuilds the page's view model when the analysis or the month changes, sends decisions and reports a refusal, cancels and unsubscribes on dispose. |
| `Features/Shared/Layout/ProblemNotices.cs`, `SyncActions.cs` | Error notification of a `BackendProblem`; the Sync button behaviour shared by the header, the banner and Settings. |
| `Features/Shared/Pickers/` | `CategoryPicker` (searchable drop-down with an accessible name) and `CategoryTag` (dot and name). |
| `Features/Overview/` | `OverviewPage` (`/overview`), `MonthChart` (stacked columns and a line, with a table of the numbers for screen readers), `CategoryDonut` (drill-down with breadcrumb; a leaf opens Transactions). |
| `Features/Transactions/` | `TransactionsPage` (`/transactions?m&cat&q`), `SplitDialog` (parts, remainder, validation), `OnlyThisOneNotice` (the undo action of the category notification). |
| `Features/FixedPayments/` | `FixedPaymentsPage` (`/fixed`), `FixedPaymentDialog`, `StatusPill`. |
| `Features/Review/` | `ReviewPage` (`/review`). |
| `Features/Categories/` | `CategoriesPage` (`/categories`) and `CategoryDialog`. |
| `Features/Settings/` | `SettingsPage` (`/settings`): connection, sync, reconnect through `IBankConnectionLauncher`, history, CSV link. |
| `Features/BankConnection/` | `BankPickerPage` (`/banks`): banks with KBC first; Connect hands the bank's address to `IBankConnectionLauncher`. |

Tests: `test/Containers/UI/Core/KbcTracker.Components.Tests` (bUnit, against a stubbed backend).
