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
- **Overview**: income, spending (fixed vs variable), savings, what's left, last months chart,
  spending by category, fixed payments still to come, biggest expenses.
- **Fixed payments**: every recurring payment with its usual day and amount, and whether it is
  paid, due, late or changed this month. ✕ = not a fixed payment, ✎ = rename.
- **Transactions**: search, filter by type/category, change a category (applies to all payments
  to that payee), ☆ = mark as fixed payment.
- **Categories**: spending per category vs. the average of previous months.
- **Settings**: sync, full re-sync, reconnect, CSV export, reset your corrections.

Your corrections are stored in `data/user-rules.json`. Bank consent lasts up to 180 days; the header
warns you 15 days before — use **Reconnect**.

## Transaction history
- The first sync after connecting uses Enable Banking's `strategy=longest`: it asks KBC for the
  oldest transaction it will release and downloads everything after it.
- Banks usually release more than 90 days only right after you log in (strong authentication).
  If **Full re-sync** still returns only recent data, click **Reconnect**, log in, and the full
  history is pulled immediately afterwards.
- Everything is stored in `./data`, and daily syncs only add to it, so your history keeps growing
  even when the bank's window is short.

## How payments are classified
Uses the ISO bank transaction code KBC sends with each transaction:
- direct debits (`IDDT`) — one series per mandate, so two contracts with the same company stay separate;
- loan instalments (`CSLN`), savings/investment plans (`SETT/SUBS`), account fees (`OPCL`);
- card payments (`CCRD`) — grouped by shop name;
- transfers — grouped by IBAN; transfers to/from accounts in your own name are **own transfers**
  (not spending, not income), as are the loan disbursement and cash deposits.

Fixed payment = direct debit / loan / savings plan / fee seen in ≥2 months, or a transfer/card payment
to the same payee in ≥3 months, about once a month, with a stable amount and day. Your ☆/✕ always win.

Categories: keywords in `appsettings.json` → `Categories` (shop name first; the longest matching
keyword wins). Anything in **Own transfers** or **Savings & investments** is excluded from spending.
