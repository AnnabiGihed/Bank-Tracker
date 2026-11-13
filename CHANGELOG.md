# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Breaking changes

- The web app no longer runs on its own: start the backend first, after setting up its PostgreSQL database and
  importing your JSON data folder once with `--import-json`
  (see [Run the backend](docs/how-to/run-the-backend.md)).
- The web app's `--sync` option, its automatic sync and its `DataDirectory`, `AutoSyncHours`, `EnableBanking` and
  `Categories` settings are gone; configure the backend instead
  (see [Configuration](docs/reference/configuration.md)).
- The daily scheduled task from `scripts/install-daily-sync.ps1` no longer syncs; remove it
  (see [Keep syncing every day](docs/how-to/sync-every-day.md)).

### Added

- An Income tab (`/income?m=yyyy-MM`) with the month's earnings (salary and other income), refunds and loan money
  received, each with its payments and a category picker and confirm button, plus the earnings of the last 12 months and
  the earnings by payer. The Overview's Income, Refunds and Loan received figures open it on their section
  (`?show=earnings|refunds|loan-received`); the Transactions tab no longer has that filter.
- A Subscriptions tab (`/subscriptions`) for recurring payments made by card that repeat monthly or yearly, with
  suggestions to accept, cost per month and per year, last and next charge and status. They leave the Fixed payments
  tab but still count as fixed payments in every total.
- A built-in category Loan received (type Transfer) for loan payouts, which used to land in Own transfers. An
  existing database gets it with the next decision.
- The Android app `KbcTracker.Mobile` (.NET MAUI Blazor Hybrid, `com.BankTracker.mobile`), built from the same
  components as the web app ([ADR-0008](docs/adr/0008-build-the-front-ends-as-shared-blazor-components.md),
  proposed). It is for development only for now: it runs in the emulator against the backend on your PC and has no
  production backend until the API moves to a server with authentication. Connecting the bank and the CSV export stay
  in the web app. See [Run the Android app](docs/how-to/run-the-android-app.md).
- New projects `KbcTracker.ViewModels` and `KbcTracker.Components`, tests for both (the components with bUnit), a
  Playwright browser test of the web app and the solution filter `KbcTracker.Core.slnf`, which builds everything
  except the Android app.
- The backend can split one payment into categorised parts ([ADR-0007](docs/adr/0007-split-a-payment-into-categorised-parts.md),
  proposed): the decisions `split` (a payment and two to a hundred parts, each an amount, a spending category and an
  optional label, adding up exactly to the payment) and `unsplit`. A split counts as spending and as confirmed, wins over
  every other category of the payment, and feeds the totals by category part by part; the payment's own category, and
  so the payee's rules and suggestions, are not changed by it. Deleting a category moves the parts that use it to its
  parent. The API's workspace gains `parts` on a transaction and `decisions` gains `parts`
  ([docs/api/openapi.json](docs/api/openapi.json)); the database gains the column `TxnSplits` (migration
  `PaymentSplits`).
- Categories are now a tree based on the COICOP 2018 household-spending classification, up to four levels deep
  (for example Transport, Running own vehicles, Car, Fuel & charging), classified by purpose and seeded by default,
  with keywords for global brands in four languages and, for Belgium (`EnableBanking:Country`, `BE` by default),
  Belgian shops and utilities. A sub-category has the type of its top-level category; deleting a category moves its
  payments, payee rules and fixed payments to its parent (to Other for a top-level one) and its sub-categories up one
  level (payee rules that fall back to Other show up in To review); built-in categories can no longer be deleted, moved or
  given another type (renaming and recolouring stay); choosing an unknown category in a decision is refused. An
  existing flat category list is upgraded once, on the next start, keeping your own categories and edits: the old
  Transport & fuel, Subscriptions and Utilities & telecom defaults are merged, and what pointed to them moves to the
  node whose keyword matches (otherwise to Transport, Streaming & subscriptions or Unsorted direct debits), with payee
  rules to confirm again in To review. The API's
  category contracts gain optional `parentId` and `coicop`, and `saveCategory` accepts `parentId`.
  In the web app the Overview chart drills down from the top-level categories with a breadcrumb, the category pickers
  and the Categories tab show the tree (with "+ sub-category" and a parent choice), and filtering Transactions on a
  category includes its sub-categories.

- One start runs the whole app: the Aspire AppHost `KbcTracker.AppHost` starts PostgreSQL in Docker, applies the
  migrations, then starts the backend and the web app, with a dashboard at <https://localhost:17180> for their
  state, logs and traces ([ADR-0005](docs/adr/0005-run-the-app-on-the-pc-with-aspire.md), proposed). On the first
  start it gives the new database the default categories by itself. Your data stays in the Docker volume
  `kbctracker-postgres`; a database you set up by hand before is not used by the AppHost
  (see [Run the backend](docs/how-to/run-the-backend.md)).
- A backend, `KbcTracker.ApiService`, that keeps your history and decisions in PostgreSQL and owns the bank key
  and the sync ([ADR-0004](docs/adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md), proposed). Its
  API serves the history, categories, the analysis the pages show, your decisions, the bank consent, the
  connection status and manual sync; [docs/api/openapi.json](docs/api/openapi.json) describes it. Outside
  Development only a loopback caller with the owner's Tailscale identity gets in.
- The backend syncs by itself while it runs, every `AutoSyncHours`, and a reconnect is followed at once by a sync
  of the whole history the bank still holds. A sync is also full whenever the last successful one is more than
  30 days old, or when an account has no history yet (for example one added at a reconnect), so a consent gap,
  a failed sync or a new account no longer leaves days that are never downloaded.
- Backend maintenance commands: `--migrate`, `--import-json <folder>` to bring over the JSON data folder of an
  older version (only read, never changed), `--initialize` to start fresh, and `--sync`. Until the database is
  imported or initialised, decisions and new bank connections are refused so that the import stays possible.
  `--initialize` takes the default categories from the code, no longer from a `Categories` section in the backend's
  `appsettings.json`, and leaves a database that already holds data alone, so it is safe to run on every start.
- Documentation in `docs/`: a get-started tutorial, how-to guides, reference pages for the configuration, tabs and
  data folder, and explanations of the classification and the architecture. Each project has a `README.md`.
- A `LICENSE` file: all rights reserved.

### Changed

- Money in is told apart better, as suggestions you confirm: payroll transfers (employer and employee markers, pay
  words, or the same payer about every month) go to Salary; refund words or a credit from someone you paid before
  go to Refunds; loan words or a credit from the lender you repay go to Loan received. Own transfers and your own
  decisions still win.
- The Overview's Income is earnings only (salary and other income). Refunds and Loan received are shown as their own
  figures when not zero and count in no total, so "Left over" is earnings − spent − saved. The month summary of
  the API gains `salary`, `refunds` and `loanReceived`.
- The category pickers show every top-level category as a bold heading you can pick, with its sub-categories
  indented below it, instead of a flat list with dashes.
- The web app is rebuilt as a Blazor Web App with Radzen components, with the same tab addresses. The charts
  no longer need the internet, the category pickers are searchable, dialogs and messages look different, and the
  "Only this one" choice after a category change appears as a notification.
- Two decisions made at the same moment, such as a double-click or two quick edits, are now both saved: the second
  waits up to five seconds for the first instead of failing at once. It still fails with `409 Conflict`, and the
  message "Another change is still being saved. Try again in a moment.", only when that wait runs out.
- The app now needs .NET 10, and is organised in the Pivot.Framework structure, the first step towards the
  Android app ([ADR-0003](docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md)): `src/Core`
  (Domain, Application), `src/Infrastructure` and `src/Containers`, with feature folders, and a `test/` tree with
  one xUnit project per source project plus end-to-end tests.
- The web app moved to `src/Containers/UI/Hosting/KbcTracker.Web`, in the `KbcTracker.slnx` solution, and is now a
  client of the backend: it stores nothing and holds no bank key, and its settings are only `Urls`,
  `AllowedHosts` and `Backend:BaseUrl`/`Backend:OwnerLogin`. Start the backend first; when it is unreachable,
  the pages say so. The bank's redirect URL stays `https://localhost:5001/callback`. See
  [Run the backend](docs/how-to/run-the-backend.md) to set up the database and import your data.
- The domain is built on Pivot.Framework's entities, aggregates and `Result`/`Error` types (ADR-0003 step 2). The
  data files keep their format, and records older versions could save with a blank id still load. A category name
  without letters or digits, a blank payee name, a change to a fixed payment that no longer exists and a request
  missing a field are now refused with a message instead of being saved or failing.
- A transaction the bank sends that cannot be read (for example with a malformed currency) is now left out, and
  every page says how many were left out, until a sync of the whole history finds none; the other transactions
  are still saved. A blank bank reference no longer becomes an id, and
  a transaction an older version stored with a blank id keeps its place on the next sync instead of being added
  twice.
- The build now runs the shared analyzers with warnings as errors, and restores packages from nuget.org and, for
  Pivot.Framework packages only, from its private feed ([ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md)).

### Removed

- The web app's `/api`, `/sync` and `/connect` addresses: the pages call the backend directly, so a bookmark to
  `/sync` or `/connect` stops working. Use the **Sync** button and the reconnect button on the Settings tab.
- The web app's JSON data folder, its `--sync` option (it now exits with code 2 and points to the backend's
  `--sync`), its own automatic sync, and its `DataDirectory`, `AutoSyncHours`, `EnableBanking` and `Categories`
  settings: the backend took them over.
- `scripts/install-daily-sync.ps1`. The backend syncs while it runs; remove the old scheduled task with
  `Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false`
  (see [Keep syncing every day](docs/how-to/sync-every-day.md)).

### Fixed

- The web app's buttons that post a form (**Connect**, **↻ Sync**, **Sync all again**) no longer answer "Web access
  denied". Because the pages ask browsers to send no referrer, browsers send an empty origin with these posts, and the
  web app took that for another site. It now relies on the browser's own same-origin marker (`Sec-Fetch-Site`), and
  still refuses writes from any other site, including another port on this PC.
- When the backend refuses a page's request (`409`), the web app now shows the backend's own reason instead of
  always blaming an empty database. When bank access is not set up, connecting a bank says which settings the
  backend needs (`EnableBanking:ApplicationId`, `EnableBanking:PrivateKeyPath`) instead of "Start again later", and
  the backend logs which bank step failed.
- When a sync, a bank connection or the bank's callback fails, the web app now returns you to the page you came
  from and shows the backend's message there once, instead of a raw error document. A failed change on a tab now
  shows the backend's message instead of its raw answer.
- When the backend refuses the web app (`403`) or is not ready (`409`, for example before `--import-json` or
  `--initialize`), the web app now says so and tells you what to check, instead of showing "Backend unavailable".
- A sync that was running while you reconnected no longer overwrites the new connection, and a sync error caused
  by a file (for example a missing private key) is shown on the pages like any other.
- Automatic categories: a keyword now matches only at the start of a word. "AD " (AD Delhaize) no longer puts
  shops such as Azad or Rad Huy in Groceries. A keyword glued to the end of a longer word (for example
  "APOTHEEK" in "STADSAPOTHEEK") no longer matches either; add the longer word as a keyword if you need it.
- Removing a fixed payment whose shop names contain accents no longer brings its suggestion back.

### Security

- `Microsoft.AspNetCore.WebUtilities`, which Pivot's messaging package brings in, is pinned to 2.3.14, the version that
  fixes the denial-of-service vulnerability CVE-2026-118750 (GHSA-6527-9fp8-r4x6). The build had failed since the
  advisory was published, because vulnerability warnings count as errors.
