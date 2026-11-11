# KbcTracker (Bank-Tracker)

Personal expense tracker for a KBC Brussels current account. .NET 10, single user, personal use only.
Built on **Pivot.Framework** as one PostgreSQL backend + API on the home PC (`KbcTracker.ApiService`, loopback
`http://127.0.0.1:5080`) used by the web app (server-rendered HTML, no Razor/Blazor, Chart.js from CDN,
`https://localhost:5001`, an API client that stores nothing) and later a MAUI Android app (SQLite cache later): see
`docs/adr/0004-…` (Proposed), which supersedes ADR-0003. No login: outside Development both hosts require a
loopback peer plus a `Tailscale-User-Login` equal to the configured owner. The Aspire AppHost
(`src/Containers/Aspire/Hosting/KbcTracker.AppHost`, ADR-0005) starts PostgreSQL (container + volume
`kbctracker-postgres`), `--migrate`, the API and the web app. Docker Compose, Tailscale Serve and backups are not
built yet.

## How data gets in
- Bank access via **Enable Banking** (PSD2 aggregator), free "restricted mode": only accounts linked in their
  Control Panel are readable. Read-only. Consent lasts 180 days → user must Reconnect.
- JWT RS256 signed with the app's private key (`.pem`, path in the backend's `EnableBanking:PrivateKeyPath`,
  kept OUTSIDE the repo — never commit it). Application ID in `EnableBanking:ApplicationId`. Only the backend
  holds them (user secrets in Development).
- Flow: web `/banks` → `/connect` → API `POST /api/v1/consent` (Enable Banking POST /auth, owner-bound state) → KBC
  login (itsme) → web `/callback?code&state` → API `GET /api/v1/consent/callback` (POST /sessions, then a full sync).
- Database setup order: `--migrate`, then `--import-json <old JSON folder>` or `--initialize` (both refuse a
  non-empty database); until then decisions and consent are refused with 409 so the import stays possible.
- `GET /accounts/{uid}/transactions` with `strategy=longest` for full syncs; incremental = last 30 days, but a
  sync is full when an account has no history (first sync, account added at a reconnect) or the last successful
  sync is missing or older than 30 days.
- **KBC only returns ~90 days** (confirmed: earliest = today − 89 days). That's why history is persisted forever.

## Files
Solution `KbcTracker.slnx` in the Pivot canonical layout: `src/Core`, `src/Infrastructure`, `src/Containers`; every
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
- `src/Containers/UI/Hosting/KbcTracker.Web` — server-rendered tabs over `BackendClient`; stores nothing.
- `src/Containers/Aspire/Hosting/KbcTracker.AppHost` — Aspire AppHost (ADR-0005); `Aspire/Defaults/KbcTracker.ServiceDefaults`
  — OpenTelemetry for both hosts (no health endpoints, no HTTP retries: they would replay the bank's one-time code).

Tests (xUnit + AwesomeAssertions + NSubstitute) mirror `src/`, plus `test/KbcTracker.E2E.Tests` (web + API in
memory). Persistence, API and E2E tests use Testcontainers PostgreSQL (Docker required); `RealDataTests` in the
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
- Category type decides totals: Expense = spending, Income, Savings (separate), Transfer (ignored).
- The user wants: clear UI, little scrolling, no manual exports/imports, everything user-correctable.

## Build / run
- Everything: `dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost` (Docker required; dashboard
  https://localhost:17180; `import-json`/`initialize` are dashboard-only resources). Without Aspire:
- Backend: `dotnet run --project src/Containers/API/KbcTracker.ApiService` (add `-- --migrate`, `-- --sync`…);
  then the web app: `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web` (needs
  `dotnet dev-certs https --trust` once). See `docs/how-to/run-the-backend.md`. `dotnet build` / `dotnet test` at the
  root use `KbcTracker.slnx`.
- Packages: versions in `Directory.Packages.props`, reasons in ADR-0002/0004. `Pivot.Framework.*` (2.1.0) come from
  the private feed in `nuget.config`; restoring them needs `PIVOT_PACKAGES_USER`/`PIVOT_PACKAGES_TOKEN` (GitHub token
  with `read:packages`, e.g. `gh auth token`). Never write the token into a file.
- Analyzers + warnings as errors for every project (`Directory.Build.props`); the web host's page classes are
  waived until they are brought up to the analyzers, at the latest 2027-06-30 (ADR-0002).
  Deviations and suppressions are listed in ADR-0002 — fix findings, don't suppress them.
- `dotnet test`. When changing detection, add a test and keep `RealDataTests` passing:
  it reads (never writes) the old JSON data in `%LOCALAPPDATA%\KbcTracker`
  (YouTube 13.99 €, rent 940 €, loan 375.50 € still suggested).

## Open ideas / known gaps
- Fixed payments and categories set in the version before `34987bb` (`user-rules.json`) are not migrated.
- Chart.js is loaded from a CDN (fails offline) — could be bundled into `wwwroot`.
- Next: Docker Compose, Tailscale Serve and backups (unverified: loopback-only port publishing, the Host header
  Serve passes, Enable Banking accepting a `*.ts.net` redirect); then MAUI and its SQLite cache.
  Later: savings account, salary/quarterly/yearly detection, Loan and Salary tabs, notifications.
  Later ideas: budgets per category.
