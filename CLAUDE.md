# KbcTracker (Bank-Tracker)

Personal expense tracker for a KBC Brussels current account. ASP.NET Core minimal API (.NET 10), server-rendered HTML
(no Razor/Blazor), Chart.js from CDN. Runs locally at https://localhost:5001. Single user, personal use only.
Built on **Pivot.Framework**, running entirely on the phone (MAUI + SQLite, no server, no login): see
`docs/adr/0003-…`, which supersedes ADR-0001. Steps 1 (canonical layout, xUnit, tabs) and 2 (domain on Pivot
types) are on `main`.

## How data gets in
- Bank access via **Enable Banking** (PSD2 aggregator), free "restricted mode": only accounts linked in their
  Control Panel are readable. Read-only. Consent lasts 180 days → user must Reconnect.
- JWT RS256 signed with the app's private key (`.pem`, path in appsettings `EnableBanking:PrivateKeyPath`,
  kept OUTSIDE the repo — never commit it). Application ID in `EnableBanking:ApplicationId`.
- Flow: `/banks` → `/connect` (POST /auth) → KBC login (itsme) → `/callback?code` (POST /sessions) → `/sync?full=true`.
- `GET /accounts/{uid}/transactions` with `strategy=longest` for full syncs; incremental = last 30 days.
- **KBC only returns ~90 days** (confirmed: earliest = today − 89 days). That's why history is persisted forever.

## Files
Solution `KbcTracker.slnx` in the Pivot canonical layout: `src/Core`, `src/Infrastructure`, `src/Containers`; every
project organised as `Features/<Feature>/…` (`Features/Shared/` for cross-feature pieces); `test/` mirrors `src/`.
Namespaces follow folders; each project file lists its feature namespaces as global usings. Tabs, one type per file.

- `src/Core/KbcTracker.Domain` (Pivot.Framework.Domain: `LightweightAggregateRoot`, `Entity`, `Result`/`Error`;
  string-valued ids derive from `Shared/ValueObjects/StronglyTypedStringId`; aggregates have `Create` (validated,
  returns `Result`) and `Rehydrate` (stored data, as is); errors in `Features/<F>/Errors`) — `Features/Transactions`
  (`Txn` + `TxnId`, value objects `Money`, `Counterparty`, `BankRecord`, `Kind`, `TxnInfo`, internal `BankFields`
  reading the ISO code from `Txn.Raw`), `Categories` (`Category` + `CategoryId` (`Other`), `CategoryType`,
  `PayeeAssignment`, `CategoryDefaults`), `FixedPayments` (`RecurringPayment` entity owned by `UserData` +
  `RecurringPaymentId`, `PayeeMatch` (`SignatureOf` builds dismissal keys), `RecurrenceRule`, `Suggestion`,
  `FixedRow`, `FixedStatus`), `BankConnection` (`SessionInfo` + `BankSessionId`,
  `RecordFullSync`/`RecordIncrementalSync`/`RecordFailure`; an incremental sync keeps the larger left-out count),
  `Overview` (`MonthSummary`), `Review` (`ReviewItem`), `Shared` (`UserData` = all user decisions, changed only
  through its behaviours; `Analysis`, the brain: classification by ISO code, series keys, merchant cleaning,
  categories, suggestions, matching, month summaries, review queue; optional `today` for tests).
- `src/Core/KbcTracker.Application` — `Features/Shared/Abstractions/IStore` (never deletes transactions),
  `Features/BankConnection` (`IBankTransactionSource` port returning `BankDownload` = readable transactions +
  skipped count, `SyncSettings`, `SyncService`).
- `src/Infrastructure/KbcTracker.Infrastructure` — `Features/BankConnection`: `EnableBankingClient` (REST + JWT),
  `EnableBankingOptions`, `EnableBankingMapper` (JSON → Txn, raw kept), `EnableBankingTransactionSource`.
- `src/Containers/UI/Hosting/KbcTracker.Web` — `Program.cs` (routes, `POST /api` switch, `--sync` mode),
  `appsettings.json`; `Features/<Tab>/<Tab>Page.cs` render each tab (`Render`), `Features/Shared/Pages/PageKit`
  (CSS, layout, formatting helpers), `Features/Shared/Storage/Store` (JSON `IStore` in the data folder, merged by
  id, daily backups, atomic writes; `Storage/Documents` map the domain to the unchanged file shapes),
  `Features/Shared/Api/ApiRequest`, `Features/BankConnection/AutoSync`, `Features/BankConnection/SyncReport` (the
  `--sync` line), `Features/Categories/CategorySeed`.
- `scripts/install-daily-sync.ps1` — Windows scheduled task running `KbcTracker.Web.dll --sync`.

Tests (xUnit + AwesomeAssertions + NSubstitute): `test/Core/…Domain.Tests` (analysis, categories, `RealDataTests`
with `[RealDataFact]`, skipped without data), `…Application.Tests`, `test/Infrastructure/…Infrastructure.Tests`,
`test/Containers/UI/Hosting/…Web.Tests` (Store), `test/KbcTracker.E2E.Tests` (in-memory host, temp data folder).
Decisions: `docs/adr/`. User docs: `docs/index.md`.

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
- `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web` (needs `dotnet dev-certs https --trust` once);
  add `-- --sync` to sync once and exit. `dotnet build` / `dotnet test` at the root use `KbcTracker.slnx`.
- Packages: versions in `Directory.Packages.props`, reasons in ADR-0002/0003. `Pivot.Framework.*` (2.0.0) come from
  the private feed in `nuget.config`; restoring them needs `PIVOT_PACKAGES_USER`/`PIVOT_PACKAGES_TOKEN` (GitHub token
  with `read:packages`, e.g. `gh auth token`). Never write the token into a file.
- Analyzers + warnings as errors for every project (`Directory.Build.props`); the web host is waived until it is
  retired (ADR-0003 step 5).
  Deviations and suppressions are listed in ADR-0002 — fix findings, don't suppress them.
- `dotnet test`. When changing detection, add a test and keep `RealDataTests` passing:
  it checks the real data in `%LOCALAPPDATA%\KbcTracker`
  (YouTube 13.99 €, rent 940 €, loan 375.50 € still suggested).

## Open ideas / known gaps
- Fixed payments and categories set in the version before `34987bb` (`user-rules.json`) are not migrated.
- Chart.js is loaded from a CDN (fails offline) — could be bundled into `wwwroot`.
- Next: steps 3–6 of ADR-0003 (EF Core SQLite persistence, mediator commands/queries, MAUI
  app, then savings account, salary/quarterly/yearly detection, Loan and Salary tabs, notifications).
  Later ideas: budgets per category.
