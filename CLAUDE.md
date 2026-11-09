# KbcTracker (Bank-Tracker)

Personal expense tracker for a KBC Brussels current account. ASP.NET Core minimal API (.NET 8), server-rendered HTML
(no Razor/Blazor), Chart.js from CDN. Runs locally at https://localhost:5001. Single user, personal use only.

## How data gets in
- Bank access via **Enable Banking** (PSD2 aggregator), free "restricted mode": only accounts linked in their
  Control Panel are readable. Read-only. Consent lasts 180 days → user must Reconnect.
- JWT RS256 signed with the app's private key (`.pem`, path in appsettings `EnableBanking:PrivateKeyPath`,
  kept OUTSIDE the repo — never commit it). Application ID in `EnableBanking:ApplicationId`.
- Flow: `/banks` → `/connect` (POST /auth) → KBC login (itsme) → `/callback?code` (POST /sessions) → `/sync?full=true`.
- `GET /accounts/{uid}/transactions` with `strategy=longest` for full syncs; incremental = last 30 days.
- **KBC only returns ~90 days** (confirmed: earliest = today − 89 days). That's why history is persisted forever.

## Files
- `Models.cs` — Txn, SessionInfo, Category/CategoryType, RecurringPayment, UserData (all user decisions).
- `Store.cs` — JSON files in the data folder (default `%LOCALAPPDATA%\KbcTracker`, `DataDirectory` setting);
  transactions are merged by id and never deleted; daily backups in `backups/`; atomic writes; Enable Banking
  JSON → Txn mapping (raw JSON kept in `Txn.Raw`). Migrates old `bin/.../data` on first start.
- `Analysis.cs` — the brain: classification by ISO bank transaction code (`bank_transaction_code.code/sub_code`),
  payee "series keys", merchant-name cleaning, category resolution, recurring **suggestions**, matching of
  accepted recurring payments, month summaries, review queue.
- `Pages.cs` — all HTML: tabs Overview / Fixed payments / Transactions / To review / Categories / Settings,
  month selector `?m=yyyy-MM`. Client JS calls `POST /api` with `{action,...}`.
- `Program.cs` — routes, `POST /api` action switch, `--sync` CLI mode.
- `SyncService.cs` — sync logic + `AutoSync` background service (every `AutoSyncHours`).
- `EnableBankingClient.cs` — REST client. `scripts/install-daily-sync.ps1` — Windows scheduled task for `--sync`.

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
- Categories: per transaction > per payee > recurring payment's category > automatic (keywords, longest match
  wins; for card payments only the shop name, never the description — it contains "Google Pay", "KBC").
  Automatic categories need user confirmation, except bank-certain kinds (loan, own transfer, fee, plan, cash).
- Category type decides totals: Expense = spending, Income, Savings (separate), Transfer (ignored).
- The user wants: clear UI, little scrolling, no manual exports/imports, everything user-correctable.

## Build / run
- `dotnet run` (needs `dotnet dev-certs https --trust` once). `dotnet run -- --sync` syncs once and exits.
- No NuGet packages beyond the shared framework — keep it that way unless there is a strong reason.
- No test project yet. When changing detection, verify against the real data in `%LOCALAPPDATA%\KbcTracker\transactions.json`
  (e.g. accepted suggestions should still include Google YouTube 13.99 €, rent 940 €, loan 375.50 €).

## Open ideas / known gaps
- Fixed payments and categories set in the version before `34987bb` (`user-rules.json`) are not migrated.
- Chart.js is loaded from a CDN (fails offline) — could be bundled into `wwwroot`.
- Possible next steps: installable PWA for Android, unit tests for `Analysis`, budgets per category,
  notifications when a fixed payment changes amount or is late, hosting on Azure.
