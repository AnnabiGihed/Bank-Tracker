# KbcTracker.Application

The use cases of KbcTracker, as Pivot.Framework mediator commands and queries. It depends only on
`KbcTracker.Domain` and Pivot.Framework.

| Feature | Types |
| --- | --- |
| `Features/BankConnection` | `SyncService` and `SyncBankCommand` (download every connected account), `GetConnectionStatusQuery`; the consent use cases (`ListBanksQuery`, `BeginBankConsentCommand`, `CompleteBankConsentCommand`, `BankConsentWorkflow`); the ports `IBankTransactionSource`, `IBankSyncPersistence`, `IBankConsentSource`, `IBankConsentPersistence` and `IConsentStateStore` |
| `Features/Transactions` | `ListTransactionsQuery`, a page of the history |
| `Features/Categories` | `ListCategoriesQuery` |
| `Features/Workspace` | `GetWorkspaceQuery` (the analysis the pages show), `ApplyDecisionCommand` (every user decision), the `IWorkspacePersistence` port and the API contracts in `Contracts/` |
| `Features/Shared` | `BackendErrors`, the errors several use cases share |

Tests: `test/Core/KbcTracker.Application.Tests` for the sync; the other use cases are tested through the API in
`test/Containers/API/KbcTracker.ApiService.Tests`.
