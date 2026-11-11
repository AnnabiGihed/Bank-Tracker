# Architecture

KbcTracker is a single-user app. It reads your account through Enable Banking, keeps every transaction it has ever
seen, and analyses the whole history each time you open a page.

It follows the Pivot.Framework structure and runs as one backend on your home PC: a PostgreSQL database and an
API, used by the web app and, later, an Android app. There is no login; outside Development, only the owner's
tailnet identity gets in ([ADR-0004][adr-0004], proposed). The backend owns the data, the bank key and the sync;
the user interfaces only call its API.

## Projects

| Layer | Project | Responsibility |
| --- | --- | --- |
| Core | `src/Core/KbcTracker.Domain` | Transactions, categories, fixed payments, the bank connection, and the `Analysis` service that classifies and summarises them. |
| Core | `src/Core/KbcTracker.Application` | Mediator commands and queries: sync, bank consent, the workspace (the analysis the pages show) and your decisions; the ports to storage and to the bank. |
| Infrastructure | `src/Infrastructure/KbcTracker.Infrastructure` | The Enable Banking client and its mapping, behind the bank ports; the reader of the old JSON data folder. |
| Infrastructure | `src/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore` | PostgreSQL persistence: the EF Core context on Pivot's base context, migrations, repositories, Pivot's unit of work, the advisory locks and the one-time import. |
| Containers | `src/Containers/API/KbcTracker.ApiService` | The backend: API controllers, the automatic sync worker, the maintenance commands, the loopback listener and the owner-identity gate. |
| Containers | `src/Containers/UI/Core/KbcTracker.Shared` | `BackendClient`, the typed API client the user interfaces share. |
| Containers | `src/Containers/UI/Hosting/KbcTracker.Web` | The web app: server-rendered pages that read and change everything through `BackendClient`. |

Inside every project, code is grouped by feature: `Features/Transactions`, `Features/Categories`,
`Features/FixedPayments`, `Features/BankConnection`, `Features/Workspace`, `Features/Overview`, `Features/Review`,
and `Features/Shared` for what several features use.

Dependencies point inward:

- The domain depends on nothing.
- The application depends on the domain.
- The infrastructure implements the application's ports.
- Only the containers wire everything together.

## Domain model

The domain is built on Pivot.Framework's building blocks:

- **Aggregates.** A transaction (`Txn`), a category, the bank connection (`SessionInfo`) and your decisions
  (`UserData`) are aggregate roots. Each has a strongly typed id (`TxnId`, `CategoryId`…) whose value is the
  string the bank or the app already uses, so stored data keeps its ids.
- **Creation.** New objects come from `Create` factories that return a `Result` with an `Error` when a value is
  invalid, for example a malformed currency. Stored objects are rebuilt with `Rehydrate`, which keeps them as
  they were saved.
- **Changes.** Your decisions change only through named behaviours of `UserData`, such as
  `AcceptFixedPayment`, `AssignPayeeCategory` or `ForgetCategory`. Accepted fixed payments are entities owned by
  `UserData`.

In PostgreSQL, ids are stored as their text value, times in UTC, the bank's raw JSON as sent, and your decisions
in `jsonb` columns with the fixed payments in their own table. Row versions catch concurrent changes.

## Data flow

1. **Consent.** In the web app you pick the bank; the backend asks Enable Banking for a consent and you log in at
   KBC. The bank sends you back to the web app, which hands the result to the backend. The backend checks that the
   request was its own, saves the session (read-only access to your linked accounts for 180 days) and syncs the
   whole history the bank still holds.
1. **Sync.** `SyncService` asks `IBankTransactionSource` for the transactions of every account. The Enable Banking
   implementation signs each request with the application's private key. A sync asks for the longest history
   when an account has no history yet (the first sync, or an account added at a reconnect; an account that stays
   empty keeps every sync full) or when the last successful sync is older than 30 days (after a consent gap or a
   failed sync), so that no day is skipped; otherwise it asks for the last 30 days. A transaction
   that cannot be read is left out and counted, and the pages show how many until a sync of the whole history
   (**Sync all again**) finds none. A database advisory lock keeps two syncs from running at once. The backend's
   worker syncs every `AutoSyncHours`.
1. **Storage.** Transactions are merged by id and never deleted, because KBC only returns about 90 days. Your
   decisions are stored apart, so a sync never overwrites them.
1. **Analysis.** For each page, the backend's workspace query runs `Analysis` over the whole history: it
   classifies every transaction, applies your decisions, and computes the fixed payments, suggestions, review
   queue and month summaries (see [How payments are classified](payment-classification.md)). Raw bank records
   and credentials never leave the backend.
1. **Pages.** The web app renders the workspace for the selected month. A decision is one API call; the backend
   applies it to `UserData` or the categories and commits it in one unit of work.

## Why the history is kept forever

The bank's 90-day window would otherwise erase last quarter's payments, and with them the evidence for quarterly
and yearly fixed payments. Keeping the history makes the analysis better over time; database backups protect it.

[adr-0004]: ../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md
