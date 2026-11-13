# KbcTracker (Bank-Tracker)

Personal expense tracker for a KBC Brussels current account. .NET 10, single user, personal use only.
Built on **Pivot.Framework** as one PostgreSQL backend + API on the home PC (`KbcTracker.ApiService`, loopback
`http://127.0.0.1:5080`) used by the web app (a Blazor Web App in Interactive Server mode on shared Radzen components,
`https://localhost:5001`, an API client that stores nothing) and the MAUI Blazor Hybrid Android app (same components,
calls the API directly, from the emulator at `http://10.0.2.2:5080`; development only, SQLite cache later): see
`docs/adr/0004-…` (Proposed), which supersedes ADR-0003, and `docs/adr/0008-…` (Proposed) for the shared components.
No login: outside Development both hosts require a
loopback peer plus a `Tailscale-User-Login` equal to the configured owner. The Aspire AppHost
(`src/Containers/Aspire/Hosting/KbcTracker.AppHost`, ADR-0005) starts PostgreSQL (container + volume
`kbctracker-postgres`), `--migrate`, the API and the web app. Production hosting is planned on a VPS with real
authentication (no tailnet); it is not built yet.

## How data gets in
- Bank access via **Enable Banking** (PSD2 aggregator), free "restricted mode": only accounts linked in their
  Control Panel are readable. Read-only. Consent lasts 180 days → user must Reconnect.
- JWT RS256 signed with the app's private key (`.pem`, path in the backend's `EnableBanking:PrivateKeyPath`,
  kept OUTSIDE the repo — never commit it). Application ID in `EnableBanking:ApplicationId`. Only the backend
  holds them (user secrets in Development).
- Flow: web `/banks` → `/connect` → API `POST /api/v1/consent` (Enable Banking POST /auth, owner-bound state) → KBC
  login (itsme) → web `/callback?code&state` → API `GET /api/v1/consent/callback` (POST /sessions, then a full sync).
- Database setup order: `--migrate`, then `--import-json <old JSON folder>` (refuses a non-empty database) or
  `--initialize` (seeds `CategoryDefaults.CreateDefaults()`, no-op exit 0 on a non-empty one); until then decisions
  and consent are refused with 409 so the import stays possible. The AppHost runs `--initialize` on every start, so
  an import into its volume only works before the first start.
- `GET /accounts/{uid}/transactions` with `strategy=longest` for full syncs; incremental = last 30 days, but a
  sync is full when an account has no history (first sync, account added at a reconnect) or the last successful
  sync is missing or older than 30 days.
- **KBC only returns ~90 days** (confirmed: earliest = today − 89 days). That's why history is persisted forever.

## Files
Solution `KbcTracker.slnx` (and `KbcTracker.Core.slnf`, the same without the Android app) in the Pivot canonical
layout: `src/Core`, `src/Infrastructure`, `src/Containers`; every
project organised as `Features/<Feature>/…` (`Features/Shared/` for cross-feature pieces); `test/` mirrors `src/`.
Namespaces follow folders; each project file lists its feature namespaces as global usings. Tabs, one type per file.
Each project's `README.md` maps its types; read it before working in that project.

- `src/Core/KbcTracker.Domain` — aggregates on Pivot types (`Create` validates and returns `Result`, `Rehydrate`
  takes stored data as is; string ids derive from `StronglyTypedStringId`); `UserData` holds every user decision and
  changes only through its behaviours; `Analysis` is the brain (classification, series keys, categories,
  suggestions, matching, month summaries, review queue).
- `src/Core/KbcTracker.Application` — mediator commands and queries: sync (`SyncService`), consent, workspace and
  decisions, with their ports; `Shared/BackendErrors.NotInitialized`.
- `src/Infrastructure/KbcTracker.Infrastructure` — Enable Banking client and mapping; `Features/Legacy` reads the old
  JSON folder for `--import-json`.
- `src/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore` — PostgreSQL context, mappings, repositories,
  advisory locks, `DataImporter`, `Migrations/` (`dotnet ef migrations add`).
- `src/Containers/API/KbcTracker.ApiService` — the backend: controllers, `AutomaticBankSync`, maintenance commands,
  `TailnetAccessMiddleware`. Contract: `docs/api/openapi.json`.
- `src/Containers/UI/Core/KbcTracker.Shared` — `BackendClient`, the typed API client.
- `src/Containers/UI/Core/KbcTracker.ViewModels` — workspace store, one view model per tab, formats, CSV export and
  backend-problem texts; plain .NET, shared by both front ends (ADR-0008).
- `src/Containers/UI/Core/KbcTracker.Components` — Razor components (Radzen) of every tab, with `.razor.cs`
  code-behind; shared by both front ends.
- `src/Containers/UI/Hosting/KbcTracker.Web` — Blazor Web App (Interactive Server) hosting the components over
  `BackendClient`, plus `/callback` and `/export.csv`; stores nothing.
- `src/Containers/UI/Hosting/KbcTracker.Mobile` — MAUI Blazor Hybrid Android app (`com.BankTracker.mobile`) hosting
  the same components; bank connect and CSV export stay in the web app.
- `src/Containers/Aspire/Hosting/KbcTracker.AppHost` — Aspire AppHost (ADR-0005); `Aspire/Defaults/KbcTracker.ServiceDefaults`
  — OpenTelemetry for both hosts (no health endpoints, no HTTP retries: they would replay the bank's one-time code).

Tests (xUnit + AwesomeAssertions + NSubstitute) mirror `src/`, plus `test/KbcTracker.E2E.Tests` (web + API in
memory, and one Playwright browser test, `Category=Browser`). The view models have unit tests and the components bUnit
tests (`test/Containers/UI/Core/KbcTracker.ViewModels.Tests`, `…Components.Tests`).
Persistence, API and E2E tests use Testcontainers PostgreSQL (Docker required); `RealDataTests` in the
domain tests are skipped without the local data. Decisions: `docs/adr/`. User docs: `docs/index.md`.

## Domain rules (decided with the user — keep them)
- KBC codes seen in real data: `CCRD/POSD` card, `IDDT/PMDD` direct debit, `CSLN/PPAY` loan instalment,
  `SETT/SUBS` investment plan, `OPCL/CHRG` account fee, `ICCN/ICCT` spare-change investing, `IRCT|ICDT/ESCT`
  transfers out, `RCDT|RRCT/ESCT` transfers in, `RCDT/CAJT` loan disbursement, `CCRD/CDPT` cash deposit.
- Account holder = most common `debtor.name` on debits; transfers to/from that name = own transfers
  (not spending/income).
- Series keys: `DD:<creditor>:<mandate>` (one per contract — two KBC insurances stay separate),
  `LOAN:<ref>`, `PLAN:`, `FEE:`, `CARD:<first 3 shop tokens>`, `TO:/FROM:<IBAN>`.
- Card labels look like `WL*GOOGLE YouTube IED02 R296 Dublin Payment with …`: shop name = text before
  "Payment with", cut at the first country+postcode token (BE1000, IE2, IEUNKNOWN…), strip processor
  prefixes (WL, SQ, SUMUP, PAYPAL…) and numbers.
- Recurring payments are **suggested, then accepted by the user** (never auto-confirmed). Suggestion =
  union of same series + fuzzy links (amount ±3% and a shared ≥5-letter token), regular cadence
  (monthly 26–35d / quarterly / yearly), stable amount; contracts (DD/loan/plan/fee) need just 2 months.
  Accepted ones match by series key OR all tokens + amount within tolerance.
- Categories: per transaction > per payee > recurring payment's category > automatic (keywords matched at the
  start of a word, so "AD " never hits "AZAD"; longest match wins; for card payments only the shop name, never
  the description — it contains "Google Pay", "KBC").
  Automatic categories need user confirmation, except bank-certain kinds (loan, own transfer, fee, plan, cash).
- Money in that no bank code decides is told apart in this order (each a suggestion the user confirms, except the
  bank-certain loan codes `RCDT/CAJT` and ISO domain `LDAS`; own transfers win over all of it): refund words (REMB,
  REFUND, TERUGBETALING, TERUGSTORTING, RETOUR, STORNO; "REFUS PRET" is a refund) > loan words (LOAN, LENING,
  KREDIET, PRET/PRÊT) or a payer the account repays a `CSLN` loan to > payroll markers (EMPLOYER and EMPLOYEE,
  SALARY, SALAIRE, WEDDE, PAYROLL, LOON, PAIE) = `income-salary` > a payer the account paid before (same IBAN or
  name), at most the largest earlier payment = refund > the same payer crediting 26–35 days apart =
  `income-salary` > income. Series keys: only card refunds (`CCRD`) use `CARD:`; other refunds and suggested loan
  payouts keep `FROM:<iban>`; `LOANIN` is for `CAJT`/`LDAS` payouts. User decisions still win. The
  Overview's Income, Refunds and Loan received figures open the Income tab (`/income?show=earnings|refunds|loan-received`)
  with exactly the payments behind them (`Analysis.Behind`, the rule `MonthSummary` adds up with); the tab also shows
  earnings by payer and the last 12 months.
- Subscriptions (tab `/subscriptions`): an accepted or suggested recurring payment whose payments are all card payments
  (`Kind.Card`) and that repeats monthly or yearly (`SubscriptionRule`, derived from the detection, no list of brands).
  A series mixing card with other kinds, a quarterly one, or one with no payment yet is not a subscription and stays on
  the Fixed payments tab. Subscriptions leave that tab only: they still count as fixed payments in every total
  (`MonthSummary.Fixed`, the Overview).
- Category type decides totals: Expense = spending, Income, Savings (separate), Transfer (ignored). Income on the
  Overview is earnings only: everything of type Income except the built-in Refunds sub-tree. Refunds and Loan received
  are shown as their own figures and count in no total (not income, not reducing spending); left over = earnings −
  spent − saved.
- Categories are a tree (ADR-0006): COICOP 2018 divisions as spending roots plus non-spending roots, max depth 4,
  children carry their root's type, classified by purpose (shampoo = Personal care, car insurance = Insurance).
  A payment stays on the deepest node that is known (STIB → Public & paid transport, "not specified" below it);
  totals roll up. Keyword tie → deeper node. Keywords: worldwide core + country pack (`EnableBanking:Country`, BE).
  Built-in ids (income, refunds, savings, own-transfers, loan-received, loan, bills, bank-fees, cash, transfers,
  other) can't be deleted; loan payouts (`RCDT/CAJT`) land in `loan-received` (Transfer type, bank-certain), and a
  stored tree without it gets it from `PostgreSqlWorkspacePersistence` like `other`; deleting a node moves its payments
  to its parent. KBC sends no merchant category code (field
  empty).
- A payment can be split into categorised parts (ADR-0007): a user decision in `UserData` (never on the bank's `Txn`),
  2–100 lines of amount (> 0, 2 decimals) + expense category + optional label, adding up exactly to the payment. A
  split wins over every other category and counts as spending and as confirmed, but `TxnInfo.Category` stays the
  unsplit category (payee logic never learns a part); money by category always comes from `TxnInfo.Parts`. A stored
  split that no longer adds up gets a "Difference" part, so totals match the bank. Actions `split`/`unsplit`.
- Later, in order: the Android app in production; instant payment detection from Google Wallet /
  KBC Mobile notifications (other wallets and banks later); receipts read and matched to payments.
- The user wants: clear UI, little scrolling, no manual exports/imports, everything user-correctable.

## Build / run
- Everything: `dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost` (Docker required; dashboard
  https://localhost:17180; `seed` (`--initialize`) fills an empty database on every start). Without Aspire:
- Backend: `dotnet run --project src/Containers/API/KbcTracker.ApiService` (add `-- --migrate`, `-- --sync`…);
  then the web app: `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web` (needs
  `dotnet dev-certs https --trust` once). See `docs/how-to/run-the-backend.md`. `dotnet build` / `dotnet test` at the
  root use `KbcTracker.slnx`, which includes the Android app and so needs the Android workload, a JDK and the Android
  SDK; `dotnet build KbcTracker.Core.slnf` / `dotnet test KbcTracker.Core.slnf` skip Android.
- Android app: start the backend, start an emulator, then
  `dotnet build src/Containers/UI/Hosting/KbcTracker.Mobile -t:Run -f net10.0-android`. When NETSDK1147 appears (the
  Android workload needs matching Mono packs), run `dotnet workload update`. See `docs/how-to/run-the-android-app.md`.
- Packages: versions in `Directory.Packages.props`, reasons in ADR-0002/0004. `Pivot.Framework.*` (2.1.0) come from
  the private feed in `nuget.config`; restoring them needs `PIVOT_PACKAGES_USER`/`PIVOT_PACKAGES_TOKEN` (GitHub token
  with `read:packages`, e.g. `gh auth token`). Never write the token into a file.
- Analyzers + warnings as errors for every project, the web host included (`Directory.Build.props`, ADR-0002).
  Deviations and suppressions are listed in ADR-0002 — fix findings, don't suppress them.
- `dotnet test`. When changing detection, add a test and keep `RealDataTests` passing:
  it reads (never writes) the old JSON data in `%LOCALAPPDATA%\KbcTracker`
  (YouTube 13.99 €, rent 940 €, loan 375.50 € still suggested).

## Open ideas / known gaps
- Fixed payments and categories set in the version before `34987bb` (`user-rules.json`) are not migrated.
- The Android app has no production backend until the API moves to a VPS with authentication (a later ADR); it only
  works in the emulator against the PC.
- Bank connect and reconnect are web-only; the Android app tells you to use the web app on the PC.
- Car-dealer payments match the keyword AUTOMOBILE and land on Car › Maintenance & repair; a purchase belongs in
  Buying vehicles (the owner sets it on the payee). Built-in roots such as Other and Loan repayments offer no
  "+ sub-category".
- Running app locks `bin/`: build and test with `python .agent-kit/verify.py --artifacts-path <temp dir>` (or
  `dotnet test KbcTracker.slnx --artifacts-path <dir>`) instead of stopping it. Back up with `pg_dump -Fc` before a data
  upgrade runs on the owner's database.
- Next: the API on a VPS with authentication (a new ADR: login for the API and both apps, domain and TLS, the bank key
  on the server, backups off the server, the bank's redirect URL); then the Android app's SQLite cache.
  Later: savings account, salary/quarterly/yearly detection, notifications.
  Later ideas: budgets per category.
