# Architecture

KbcTracker is a single-user app. It reads your account through Enable Banking, keeps every transaction it has ever
seen, and analyses the whole history each time you open a page.

It follows the Pivot.Framework structure and runs entirely on your device: there is no server and no login
([ADR-0003][adr-0003]).

## Projects

| Layer | Project | Responsibility |
| --- | --- | --- |
| Core | `src/Core/KbcTracker.Domain` | Transactions, categories, fixed payments, the bank connection, and the `Analysis` service that classifies and summarises them. |
| Core | `src/Core/KbcTracker.Application` | Use cases, such as the bank sync, and the ports they need: `IStore` for storage, `IBankTransactionSource` for the bank. |
| Infrastructure | `src/Infrastructure/KbcTracker.Infrastructure` | The Enable Banking client and its JSON mapping, behind `IBankTransactionSource`. |
| Containers | `src/Containers/UI/Hosting/KbcTracker.Web` | The local web app: server-rendered pages, `POST /api`, the JSON storage, the auto-sync timer. |

Inside every project, code is grouped by feature: `Features/Transactions`, `Features/Categories`,
`Features/FixedPayments`, `Features/BankConnection`, `Features/Overview`, `Features/Review`, and `Features/Shared`
for what several features use.

Dependencies point inward:

- The domain depends on nothing.
- The application depends on the domain.
- The infrastructure implements the application's ports.
- Only the container wires everything together.

The planned Android app is a second container over the same layers.

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

The web host stores the aggregates as JSON documents with the same shape as before, so existing data folders load
unchanged.

## Data flow

1. **Consent.** You log in at KBC through Enable Banking. The app receives a session that gives read-only access
   to your linked accounts for 180 days.
1. **Sync.** `SyncService` asks `IBankTransactionSource` for the transactions of every account. The Enable Banking
   implementation signs each request with the application's private key. The first sync asks for the longest
   history; later syncs ask for the last 30 days. A transaction that cannot be read is left out and counted, and
   the pages show how many until a sync of the whole history (**Sync all again**) finds none; the rest of the sync
   is saved.
1. **Storage.** The store merges the transactions by id and never deletes any, because KBC only returns about
   90 days. Your decisions are stored apart, so a sync never overwrites them.
1. **Analysis.** `Analysis` classifies every transaction, applies your decisions, and computes the fixed payments,
   suggestions, review queue and month summaries (see
   [How payments are classified](payment-classification.md)).
1. **Pages.** Each tab renders the analysis for the selected month.

## Why the history is kept forever

The bank's 90-day window would otherwise erase last quarter's payments, and with them the evidence for quarterly
and yearly fixed payments. Keeping the history makes the analysis better over time; daily backups protect it.

[adr-0003]: ../adr/0003-build-on-pivot-framework-running-on-the-phone.md
