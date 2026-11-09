# KbcTracker — your KBC Brussels transactions, fixed payments & spending dashboard

Runs locally on your PC. Data is pulled read-only from KBC Brussels through
Enable Banking (free "restricted mode" for your own accounts) and stored in `./data`
next to the app — nothing leaves your machine except the calls to Enable Banking/KBC.

## One-time setup (≈10 min)
1. Sign up at https://enablebanking.com/cp (Control Panel).
2. Applications → **Register new application**
   - Environment: **Production**
   - Redirect URL: `https://localhost:5001/callback`
   - Let it generate the key → the `.pem` private key downloads. Keep it safe.
3. On the new (Inactive) application click **Activate by linking accounts**,
   pick **KBC Brussels**, log in with itsme / card reader. App becomes *Active (restricted)*.
4. Edit `appsettings.json`: put the **Application ID** and the path to the `.pem`.
5. `dotnet dev-certs https --trust` (once), then `dotnet run`.
6. Open https://localhost:5001 → **Connect KBC Brussels** → log in again → transactions download.

## Using it
Pick a month at the top (‹ › or the drop-down); every tab follows it.
- **Overview**: income, spending (fixed vs variable), savings, what's left, last months, spending by category,
  fixed payments still to come, biggest expenses. A banner tells you when something needs your attention.
- **Fixed payments**: *suggestions* found in your history (✓ = it's a fixed payment, ✕ = not), then your
  confirmed fixed payments with their status this month (paid / due / late / amount changed). ✎ edits name,
  category, amount and rhythm (monthly / quarterly / yearly).
- **Transactions**: search and filter. Change a category → it applies to all payments of that payee, with an
  "Only this one" option. Dashed category = automatic, ✓ confirms it. ☆ = mark as fixed payment.
- **To review**: every payee whose automatic category you haven't confirmed yet; confirm one by one or all at once.
- **Categories**: create / edit / delete categories: name, colour, keywords, and how they count
  (spending, income, savings, own transfer). Totals per month vs. previous months.
- **Settings**: bank connection, history stored, data folder, CSV export.

## Your history is kept, independently from KBC
KBC only shares the last ~90 days. The app stores every transaction it has ever downloaded and never deletes
any, so history keeps growing.
- Data folder: `%LOCALAPPDATA%\KbcTracker` (change with `DataDirectory` in appsettings.json). Data from older
  versions (in `bin\…\data`) is moved there automatically on first start.
- Daily backup of the transactions in `backups\` (last 30 kept).
- While the app runs it syncs every 6 hours (`AutoSyncHours`).
- To sync every day even when the app is closed, run once:
  `powershell -ExecutionPolicy Bypass -File .\scripts\install-daily-sync.ps1`
  (builds the app to `%LOCALAPPDATA%\KbcTracker\app` and registers a Windows scheduled task running `--sync`).
- The only manual step left: reconnect every 180 days (the app warns you 15 days before).

## How payments are classified
Uses the ISO bank transaction code KBC sends with each transaction:
- direct debits (`IDDT`) — one payee per mandate, so two contracts with the same company stay separate;
- loan instalments (`CSLN`), savings plans (`SETT/SUBS`), account fees (`OPCL`), spare-change investing;
- card payments (`CCRD`) — grouped by shop name, cleaned from the bank label
  (`WL*GOOGLE YouTube IED02 R296 Dublin` and `GOOGLE *YouTube IEUNKNOWN` both become *Google YouTube*);
- transfers — grouped by IBAN; transfers to/from your own name are own transfers.

**Fixed-payment suggestions**: payments are grouped by payee, and payments with almost the same amount (±3%) that
share a distinctive shop word are merged too. A group is suggested when it comes back on a regular rhythm
(monthly, quarterly or yearly) with a stable amount — or, for direct debits, loans, savings plans and fees, as soon
as it appears in 2 months. Once accepted, new payments are matched by payee *or* by shop words + amount, so a
changed bank label doesn't break it.

**Categories**: your choice per transaction > per payee > the fixed payment's category > automatic (keywords,
longest match wins; for card payments only the shop name is used). Categories the bank code makes certain
(loan, own transfers, fees, savings plan, cash) are confirmed automatically.

