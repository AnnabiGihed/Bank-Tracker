# KbcTracker.Application

The use cases of KbcTracker, as Pivot.Framework mediator commands and queries. It depends only on
`KbcTracker.Domain` and Pivot.Framework.

| Feature | Types |
| --- | --- |
| `Features/BankConnection` | `SyncService` and `SyncBankCommand` (download every connected account), `GetConnectionStatusQuery`; the consent use cases (`ListBanksQuery`, `BeginBankConsentCommand`, `CompleteBankConsentCommand`, `BankConsentWorkflow`); the ports `IBankTransactionSource`, `IBankSyncPersistence`, `IBankConsentSource`, `IBankConsentPersistence` and `IConsentStateStore` |
| `Features/Transactions` | `ListTransactionsQuery`, a page of the history |
| `Features/Categories` | `ListCategoriesQuery`; category edits, which place a category in the tree and move its references on delete, are part of `Features/Workspace` (`CategoryDecisions`) |
| `Features/Workspace` | `GetWorkspaceQuery` (the analysis the pages show), `ApplyDecisionCommand` (every user decision), the `IWorkspacePersistence` port and the API contracts in `Contracts/` |
| `Features/Shared` | `BackendErrors`, the errors several use cases share |

Tests: `test/Core/KbcTracker.Application.Tests` for the sync; the other use cases are tested through the API in
`test/Containers/API/KbcTracker.ApiService.Tests`.
